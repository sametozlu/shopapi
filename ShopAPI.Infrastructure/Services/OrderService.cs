using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using ShopAPI.Application;
using ShopAPI.Domain;

namespace ShopAPI.Infrastructure;

public class OrderService : IOrderService
{
    private readonly AppDbContext _dbContext;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _cache;

    public OrderService(
        AppDbContext dbContext,
        IPaymentGateway paymentGateway,
        IConfiguration configuration,
        IDistributedCache cache)
    {
        _dbContext = dbContext;
        _paymentGateway = paymentGateway;
        _configuration = configuration;
        _cache = cache;
    }

    public async Task<OrderDto?> CreateOrderAsync(Guid userId, CreateOrderRequest request)
    {
        var cartItems = await _dbContext.CartItems
            .Include(x => x.Product)
            .Include(x => x.ProductVariant)
            .Where(x => x.UserId == userId)
            .ToListAsync();
        if (!cartItems.Any()) return null;
        var address = await _dbContext.UserAddresses.FirstOrDefaultAsync(x => x.Id == request.ShippingAddressId && x.UserId == userId);
        if (address is null) throw new InvalidOperationException("Shipping address not found.");

        foreach (var cartItem in cartItems)
        {
            if (cartItem.Product is null || cartItem.Product.Stock < cartItem.Quantity) return null;
            if (cartItem.ProductVariant is not null && cartItem.ProductVariant.Stock < cartItem.Quantity) return null;
        }

        var subtotal = cartItems.Sum(x => (x.ProductVariant?.OverridePrice ?? x.Product!.Price) * x.Quantity);
        var shippingCost = request.ShippingMethod == ShippingMethod.Express ? 119m : (subtotal >= 1500m ? 0m : 49m);
        decimal discount = 0m;
        string? couponCode = null;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var coupon = await _dbContext.Coupons.FirstOrDefaultAsync(x =>
                x.Code == request.CouponCode.Trim().ToUpper() &&
                x.IsActive &&
                x.ExpiresAt > DateTime.UtcNow);
            if (coupon is not null && subtotal >= coupon.MinOrderAmount)
            {
                discount = coupon.FixedAmount ?? (subtotal * ((coupon.Percentage ?? 0m) / 100m));
                discount = Math.Min(discount, subtotal);
                couponCode = coupon.Code;
            }
        }

        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.Pending,
            ShippingAddressId = address.Id,
            ShippingMethod = request.ShippingMethod,
            ShippingCost = shippingCost,
            DiscountAmount = discount,
            CouponCode = couponCode
        };

        foreach (var cartItem in cartItems)
        {
            var product = cartItem.Product!;
            product.Stock -= cartItem.Quantity;
            if (cartItem.ProductVariant is not null) cartItem.ProductVariant.Stock -= cartItem.Quantity;
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductVariantId = cartItem.ProductVariantId,
                VariantName = cartItem.ProductVariant?.Name,
                ProductName = product.Name,
                UnitPrice = cartItem.ProductVariant?.OverridePrice ?? product.Price,
                Quantity = cartItem.Quantity
            });
        }

        order.TotalAmount = subtotal + shippingCost - discount;
        _dbContext.Orders.Add(order);
        _dbContext.CartItems.RemoveRange(cartItems);
        AddOutbox("order.created", new
        {
            orderId = order.Id,
            userId,
            total = order.TotalAmount,
            shippingMethod = order.ShippingMethod.ToString(),
            couponCode = order.CouponCode
        });
        await _dbContext.SaveChangesAsync();
        await InvalidateProductCacheAsync(cartItems.Select(x => x.ProductId));

        return await GetOrderDtoByIdAsync(order.Id);
    }

    public async Task<List<OrderDto>> GetMyOrdersAsync(Guid userId) =>
        (await _dbContext.Orders.Include(x => x.Items).Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).ToListAsync())
        .Select(MapOrder).ToList();

    public async Task<List<OrderDto>> GetAllOrdersAsync() =>
        (await _dbContext.Orders.Include(x => x.Items).OrderByDescending(x => x.CreatedAt).ToListAsync())
        .Select(MapOrder).ToList();

    public async Task<PaymentStartResponse?> PayOrderAsync(Guid userId, Guid orderId)
    {
        var order = await _dbContext.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == orderId && x.UserId == userId);
        if (order is null || order.Status != OrderStatus.Pending) return null;

        var provider = _configuration["Payments:Provider"]?.ToLowerInvariant() ?? "mock";
        if (provider == "stripe")
            return await StartStripeCheckoutAsync(userId, order);

        var payment = await _paymentGateway.ChargeAsync(userId, order.TotalAmount);
        await RecordPaymentAsync(order, payment);
        if (!payment.Success)
            throw new InvalidOperationException(payment.ErrorMessage ?? "Payment failed.");

        order.Status = OrderStatus.Paid;
        AddOutbox("payment.succeeded", new
        {
            orderId = order.Id,
            transactionId = payment.TransactionId,
            amount = order.TotalAmount
        });
        await _dbContext.SaveChangesAsync();
        return new PaymentStartResponse("completed", MapOrder(order), null, null);
    }

    public async Task<OrderDto?> ConfirmPaymentAsync(Guid userId, Guid orderId, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;

        var order = await _dbContext.Orders.Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == orderId && x.UserId == userId);
        if (order is null) return null;
        if (order.Status == OrderStatus.Paid) return MapOrder(order);
        if (order.Status != OrderStatus.Pending) return null;

        var provider = _configuration["Payments:Provider"]?.ToLowerInvariant() ?? "mock";
        if (provider != "stripe")
            return null;

        var secretKey = _configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey)) return null;

        Stripe.StripeConfiguration.ApiKey = secretKey;
        var session = await new Stripe.Checkout.SessionService().GetAsync(sessionId);
        if (!string.Equals(session.Metadata.GetValueOrDefault("orderId"), orderId.ToString(), StringComparison.OrdinalIgnoreCase))
            return null;
        if (!string.Equals(session.Metadata.GetValueOrDefault("userId"), userId.ToString(), StringComparison.OrdinalIgnoreCase))
            return null;
        if (!string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
            return null;

        var tx = await _dbContext.PaymentTransactions
            .FirstOrDefaultAsync(x => x.OrderId == orderId && x.ProviderTransactionId == sessionId);
        if (tx is not null)
        {
            tx.Status = PaymentStatus.Succeeded;
            tx.ProcessedAt = DateTime.UtcNow;
            tx.FailureReason = null;
        }
        else
        {
            _dbContext.PaymentTransactions.Add(new PaymentTransaction
            {
                OrderId = order.Id,
                Provider = "stripe",
                ProviderTransactionId = sessionId,
                Amount = order.TotalAmount,
                Currency = "TRY",
                Status = PaymentStatus.Succeeded,
                ProcessedAt = DateTime.UtcNow
            });
        }

        order.Status = OrderStatus.Paid;
        AddOutbox("payment.succeeded", new { orderId = order.Id, transactionId = sessionId, amount = order.TotalAmount });
        await _dbContext.SaveChangesAsync();
        return MapOrder(order);
    }

    private async Task<PaymentStartResponse> StartStripeCheckoutAsync(Guid userId, Order order)
    {
        var frontendUrl = (_configuration["Stripe:FrontendUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var successUrl = $"{frontendUrl}/?payment=success&orderId={order.Id}&session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{frontendUrl}/?payment=cancel&orderId={order.Id}";

        var lineItems = new List<CheckoutLineItem>
        {
            new(
                $"ShopAPI Order {order.Id.ToString()[..8]}",
                (long)Math.Round(order.TotalAmount * 100m, MidpointRounding.AwayFromZero),
                1)
        };

        var session = await _paymentGateway.CreateCheckoutSessionAsync(new CheckoutSessionRequest(
            order.Id,
            userId,
            order.TotalAmount,
            "TRY",
            lineItems,
            successUrl,
            cancelUrl));

        if (!session.Success || string.IsNullOrWhiteSpace(session.Url) || string.IsNullOrWhiteSpace(session.SessionId))
            throw new InvalidOperationException(session.ErrorMessage ?? "Stripe checkout session could not be created.");

        _dbContext.PaymentTransactions.Add(new PaymentTransaction
        {
            OrderId = order.Id,
            Provider = "stripe",
            ProviderTransactionId = session.SessionId,
            Amount = order.TotalAmount,
            Currency = "TRY",
            Status = PaymentStatus.Pending,
            ProcessedAt = DateTime.UtcNow
        });
        AddOutbox("payment.checkout_started", new { orderId = order.Id, sessionId = session.SessionId });
        await _dbContext.SaveChangesAsync();

        return new PaymentStartResponse("stripe_checkout", null, session.Url, session.SessionId);
    }

    private async Task RecordPaymentAsync(Order order, PaymentResult payment)
    {
        var transaction = new PaymentTransaction
        {
            OrderId = order.Id,
            Provider = payment.Provider,
            ProviderTransactionId = payment.TransactionId ?? $"MOCK-{Guid.NewGuid():N}",
            Amount = order.TotalAmount,
            Currency = "TRY",
            Status = payment.Success ? PaymentStatus.Succeeded : PaymentStatus.Failed,
            FailureReason = payment.ErrorMessage,
            ProcessedAt = DateTime.UtcNow
        };
        _dbContext.PaymentTransactions.Add(transaction);

        if (!payment.Success)
        {
            AddOutbox("payment.failed", new
            {
                orderId = order.Id,
                transactionId = transaction.ProviderTransactionId,
                reason = payment.ErrorMessage
            });
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<OrderDto?> HandlePaymentWebhookAsync(PaymentWebhookRequest request)
    {
        var tx = await _dbContext.PaymentTransactions
            .Include(x => x.Order)
            .ThenInclude(x => x!.Items)
            .FirstOrDefaultAsync(x => x.ProviderTransactionId == request.ProviderTransactionId);
        if (tx is null || tx.Order is null) return null;

        var success = request.Status.Equals("succeeded", StringComparison.OrdinalIgnoreCase)
            || request.Status.Equals("paid", StringComparison.OrdinalIgnoreCase);

        tx.Status = success ? PaymentStatus.Succeeded : PaymentStatus.Failed;
        tx.FailureReason = success ? null : request.FailureReason;
        tx.ProcessedAt = DateTime.UtcNow;

        if (success && tx.Order.Status == OrderStatus.Pending)
        {
            tx.Order.Status = OrderStatus.Paid;
            AddOutbox("payment.webhook_succeeded", new
            {
                orderId = tx.Order.Id,
                transactionId = tx.ProviderTransactionId
            });
        }
        else if (!success)
        {
            AddOutbox("payment.webhook_failed", new
            {
                orderId = tx.Order.Id,
                transactionId = tx.ProviderTransactionId,
                reason = request.FailureReason
            });
        }

        await _dbContext.SaveChangesAsync();
        return MapOrder(tx.Order);
    }

    public async Task<OrderDto?> CancelOrderAsync(Guid userId, Guid orderId, bool isAdmin)
    {
        var query = _dbContext.Orders.Include(x => x.Items).AsQueryable();
        if (!isAdmin)
            query = query.Where(x => x.UserId == userId);

        var order = await query.FirstOrDefaultAsync(x => x.Id == orderId);
        if (order is null) return null;
        if (order.Status is OrderStatus.Cancelled or OrderStatus.Shipped) return null;

        await RestoreOrderStockAsync(order);
        order.Status = OrderStatus.Cancelled;
        AddOutbox("order.cancelled", new { orderId = order.Id, byAdmin = isAdmin });
        await _dbContext.SaveChangesAsync();
        await InvalidateProductCacheAsync(order.Items.Select(x => x.ProductId));
        return MapOrder(order);
    }

    public async Task<OrderDto?> UpdateStatusAsync(Guid orderId, OrderStatus status)
    {
        var order = await _dbContext.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == orderId);
        if (order is null) return null;

        if (!IsValidTransition(order.Status, status))
            throw new InvalidOperationException($"Invalid status transition: {order.Status} -> {status}");

        var previousStatus = order.Status;
        if (status == OrderStatus.Cancelled && previousStatus is not OrderStatus.Cancelled)
            await RestoreOrderStockAsync(order);

        order.Status = status;
        AddOutbox("order.status_changed", new { orderId = order.Id, status = status.ToString() });
        await _dbContext.SaveChangesAsync();
        if (status == OrderStatus.Cancelled)
            await InvalidateProductCacheAsync(order.Items.Select(x => x.ProductId));
        return MapOrder(order);
    }

    public async Task<OrderDto?> ConfirmStripeWebhookAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;

        var secretKey = _configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey)) return null;

        Stripe.StripeConfiguration.ApiKey = secretKey;
        var session = await new Stripe.Checkout.SessionService().GetAsync(sessionId);
        if (!string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!Guid.TryParse(session.Metadata.GetValueOrDefault("orderId"), out var orderId))
            return null;

        var order = await _dbContext.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == orderId);
        if (order is null) return null;
        if (order.Status == OrderStatus.Paid) return MapOrder(order);
        if (order.Status != OrderStatus.Pending) return null;

        var tx = await _dbContext.PaymentTransactions
            .FirstOrDefaultAsync(x => x.OrderId == orderId && x.ProviderTransactionId == sessionId);
        if (tx is not null)
        {
            tx.Status = PaymentStatus.Succeeded;
            tx.ProcessedAt = DateTime.UtcNow;
            tx.FailureReason = null;
        }
        else
        {
            _dbContext.PaymentTransactions.Add(new PaymentTransaction
            {
                OrderId = order.Id,
                Provider = "stripe",
                ProviderTransactionId = sessionId,
                Amount = order.TotalAmount,
                Currency = "TRY",
                Status = PaymentStatus.Succeeded,
                ProcessedAt = DateTime.UtcNow
            });
        }

        order.Status = OrderStatus.Paid;
        AddOutbox("payment.webhook_succeeded", new { orderId = order.Id, transactionId = sessionId });
        await _dbContext.SaveChangesAsync();
        return MapOrder(order);
    }

    private async Task RestoreOrderStockAsync(Order order)
    {
        foreach (var item in order.Items)
        {
            var product = await _dbContext.Products.FindAsync(item.ProductId);
            if (product is not null)
                product.Stock += item.Quantity;

            if (item.ProductVariantId.HasValue)
            {
                var variant = await _dbContext.ProductVariants.FindAsync(item.ProductVariantId.Value);
                if (variant is not null)
                    variant.Stock += item.Quantity;
            }
        }
    }

    private async Task InvalidateProductCacheAsync(IEnumerable<Guid> productIds)
    {
        foreach (var productId in productIds.Distinct())
            await _cache.RemoveAsync($"product:{productId}");
    }

    private static bool IsValidTransition(OrderStatus current, OrderStatus next)
    {
        if (current == next) return true;

        return current switch
        {
            OrderStatus.Pending => next is OrderStatus.Paid or OrderStatus.Cancelled,
            OrderStatus.Paid => next is OrderStatus.Shipped or OrderStatus.Cancelled,
            _ => false
        };
    }

    private async Task<OrderDto?> GetOrderDtoByIdAsync(Guid id)
    {
        var order = await _dbContext.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        return order is null ? null : MapOrder(order);
    }

    private static OrderDto MapOrder(Order order) => new(
        order.Id,
        order.UserId,
        order.TotalAmount,
        order.ShippingCost,
        order.DiscountAmount,
        order.CouponCode,
        order.ShippingMethod,
        order.ShippingAddressId,
        order.Status,
        order.CreatedAt,
        order.Items.Select(i => new OrderItemDto(i.Id, i.ProductId, i.ProductVariantId, i.ProductName, i.VariantName, i.UnitPrice, i.Quantity)).ToList()
    );

    private void AddOutbox(string eventType, object payload)
    {
        _dbContext.OutboxEvents.Add(new OutboxEvent
        {
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload),
            CreatedAt = DateTime.UtcNow
        });
    }
}
