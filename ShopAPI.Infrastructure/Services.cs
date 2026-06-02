using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShopAPI.Application;
using ShopAPI.Domain;

namespace ShopAPI.Infrastructure;

public class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly ITokenService _tokenService;

    public AuthService(AppDbContext dbContext, ITokenService tokenService)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Length < 2)
            throw new ArgumentException("Full name must be at least 2 characters.");
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            throw new ArgumentException("Email is invalid.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new ArgumentException("Password must be at least 6 characters.");

        var existing = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == request.Email.ToLower());
        if (existing is not null)
        {
            throw new InvalidOperationException("Email already in use.");
        }

        var role = request.Email.EndsWith("@admin.local", StringComparison.OrdinalIgnoreCase)
            ? UserRole.Admin
            : UserRole.Customer;

        var user = new AppUser
        {
            FullName = request.FullName,
            Email = request.Email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return _tokenService.IssueTokens(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == request.Email.ToLower())
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        return _tokenService.IssueTokens(user);
    }

    public Task<AuthResponse?> RefreshAsync(RefreshTokenRequest request) =>
        _tokenService.RefreshAsync(request.RefreshToken);

    public Task RevokeAsync(RefreshTokenRequest request) =>
        _tokenService.RevokeRefreshTokenAsync(request.RefreshToken);
}

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _dbContext;

    public CategoryService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CategoryDto>> GetAllAsync() =>
        (await _dbContext.Categories.OrderBy(x => x.Name).ToListAsync()).Select(MapCategory).ToList();

    public async Task<CategoryDto> CreateAsync(CategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 2)
            throw new ArgumentException("Category name must be at least 2 characters.");
        if (string.IsNullOrWhiteSpace(request.Slug) || request.Slug.Length < 2)
            throw new ArgumentException("Category slug must be at least 2 characters.");

        var slug = request.Slug.ToLower();
        var slugExists = await _dbContext.Categories.AnyAsync(x => x.Slug == slug);
        if (slugExists) throw new InvalidOperationException("Category slug already exists.");

        var category = new Category { Name = request.Name, Slug = slug };
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();
        return MapCategory(category);
    }

    public async Task<CategoryDto?> UpdateAsync(Guid id, CategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 2)
            throw new ArgumentException("Category name must be at least 2 characters.");
        if (string.IsNullOrWhiteSpace(request.Slug) || request.Slug.Length < 2)
            throw new ArgumentException("Category slug must be at least 2 characters.");

        var category = await _dbContext.Categories.FindAsync(id);
        if (category is null) return null;
        var slug = request.Slug.ToLower();
        var slugExists = await _dbContext.Categories.AnyAsync(x => x.Id != id && x.Slug == slug);
        if (slugExists) throw new InvalidOperationException("Category slug already exists.");
        category.Name = request.Name;
        category.Slug = slug;
        await _dbContext.SaveChangesAsync();
        return MapCategory(category);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var category = await _dbContext.Categories.FindAsync(id);
        if (category is null) return false;
        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    private static CategoryDto MapCategory(Category category) => new(category.Id, category.Name, category.Slug);
}

public class ProductService : IProductService
{
    private readonly AppDbContext _dbContext;

    public ProductService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<ProductDto>> GetAllAsync(ProductQuery query)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);
        var q = _dbContext.Products
            .Include(x => x.Category)
            .Include(x => x.Variants)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(x => x.Name.Contains(query.Search));
        if (query.CategoryId.HasValue)
            q = q.Where(x => x.CategoryId == query.CategoryId.Value);
        if (query.MinPrice.HasValue)
            q = q.Where(x => x.Price >= query.MinPrice.Value);
        if (query.MaxPrice.HasValue)
            q = q.Where(x => x.Price <= query.MaxPrice.Value);
        if (query.IsActive.HasValue)
            q = q.Where(x => x.IsActive == query.IsActive.Value);

        q = (query.SortBy?.ToLower(), query.Desc) switch
        {
            ("price", true) => q.OrderByDescending(x => x.Price),
            ("price", false) => q.OrderBy(x => x.Price),
            ("stock", true) => q.OrderByDescending(x => x.Stock),
            ("stock", false) => q.OrderBy(x => x.Stock),
            ("createdat", true) => q.OrderByDescending(x => x.Id),
            ("createdat", false) => q.OrderBy(x => x.Id),
            (_, true) => q.OrderByDescending(x => x.Name),
            _ => q.OrderBy(x => x.Name)
        };

        var totalCount = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<ProductDto>(items.Select(MapProduct).ToList(), page, pageSize, totalCount);
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id)
    {
        var product = await _dbContext.Products
            .Include(x => x.Category)
            .Include(x => x.Variants)
            .FirstOrDefaultAsync(x => x.Id == id);
        return product is null ? null : MapProduct(product);
    }

    public async Task<ProductDto?> CreateAsync(ProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 2)
            throw new ArgumentException("Product name must be at least 2 characters.");
        if (request.Price <= 0)
            throw new ArgumentException("Price must be greater than 0.");
        if (request.Stock < 0)
            throw new ArgumentException("Stock cannot be negative.");

        var categoryExists = await _dbContext.Categories.AnyAsync(x => x.Id == request.CategoryId);
        if (!categoryExists) return null;

        var product = new Product
        {
            Name = request.Name,
            Price = request.Price,
            Stock = request.Stock,
            CategoryId = request.CategoryId,
            IsActive = request.IsActive
        };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();
        return await GetByIdAsync(product.Id);
    }

    public async Task<ProductDto?> UpdateAsync(Guid id, ProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 2)
            throw new ArgumentException("Product name must be at least 2 characters.");
        if (request.Price <= 0)
            throw new ArgumentException("Price must be greater than 0.");
        if (request.Stock < 0)
            throw new ArgumentException("Stock cannot be negative.");

        var product = await _dbContext.Products.FindAsync(id);
        if (product is null) return null;

        product.Name = request.Name;
        product.Price = request.Price;
        product.Stock = request.Stock;
        product.CategoryId = request.CategoryId;
        product.IsActive = request.IsActive;
        await _dbContext.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var product = await _dbContext.Products.FindAsync(id);
        if (product is null) return false;
        _dbContext.Products.Remove(product);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<ProductVariantDto>> GetVariantsAsync(Guid productId)
    {
        var variants = await _dbContext.ProductVariants
            .Where(x => x.ProductId == productId)
            .OrderBy(x => x.Name)
            .ToListAsync();
        return variants.Select(MapVariant).ToList();
    }

    public async Task<ProductVariantDto?> CreateVariantAsync(Guid productId, ProductVariantRequest request)
    {
        var product = await _dbContext.Products.FindAsync(productId);
        if (product is null) return null;

        var sku = request.Sku.Trim().ToUpperInvariant();
        if (await _dbContext.ProductVariants.AnyAsync(x => x.Sku == sku))
            throw new InvalidOperationException("Variant SKU already exists.");

        var variant = new ProductVariant
        {
            ProductId = productId,
            Sku = sku,
            Name = request.Name,
            OverridePrice = request.OverridePrice,
            Stock = request.Stock,
            IsActive = request.IsActive
        };
        _dbContext.ProductVariants.Add(variant);
        await _dbContext.SaveChangesAsync();
        return MapVariant(variant);
    }

    public async Task<ProductVariantDto?> UpdateVariantAsync(Guid productId, Guid variantId, ProductVariantRequest request)
    {
        var variant = await _dbContext.ProductVariants.FirstOrDefaultAsync(x => x.Id == variantId && x.ProductId == productId);
        if (variant is null) return null;
        var sku = request.Sku.Trim().ToUpperInvariant();
        if (await _dbContext.ProductVariants.AnyAsync(x => x.Id != variantId && x.Sku == sku))
            throw new InvalidOperationException("Variant SKU already exists.");
        variant.Sku = sku;
        variant.Name = request.Name;
        variant.OverridePrice = request.OverridePrice;
        variant.Stock = request.Stock;
        variant.IsActive = request.IsActive;
        await _dbContext.SaveChangesAsync();
        return MapVariant(variant);
    }

    private static ProductDto MapProduct(Product product) => new(
        product.Id,
        product.Name,
        product.Price,
        product.Stock,
        product.IsActive,
        product.CategoryId,
        product.Category is null ? null : new CategoryDto(product.Category.Id, product.Category.Name, product.Category.Slug),
        product.Variants.Select(MapVariant).ToList()
    );

    private static ProductVariantDto MapVariant(ProductVariant v) => new(v.Id, v.Sku, v.Name, v.OverridePrice, v.Stock, v.IsActive);
}

public class CartService : ICartService
{
    private readonly AppDbContext _dbContext;

    public CartService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CartItemDto>> GetMyCartAsync(Guid userId) =>
        (await _dbContext.CartItems
            .Include(x => x.Product)
            .ThenInclude(x => x!.Category)
            .Include(x => x.Product)
            .ThenInclude(x => x!.Variants)
            .Include(x => x.ProductVariant)
            .Where(x => x.UserId == userId)
            .ToListAsync())
        .Select(x => new CartItemDto(
            x.Id,
            x.ProductId,
            x.ProductVariantId,
            x.Quantity,
            x.Product is null ? null : ProductServiceMap.MapProduct(x.Product),
            x.ProductVariant is null ? null : new ProductVariantDto(x.ProductVariant.Id, x.ProductVariant.Sku, x.ProductVariant.Name, x.ProductVariant.OverridePrice, x.ProductVariant.Stock, x.ProductVariant.IsActive)
        ))
        .ToList();

    public async Task<bool> AddItemAsync(Guid userId, AddCartItemRequest request)
    {
        if (request.Quantity <= 0) throw new ArgumentException("Quantity must be greater than 0.");
        var product = await _dbContext.Products.FindAsync(request.ProductId);
        if (product is null || !product.IsActive || product.Stock < request.Quantity) return false;
        ProductVariant? variant = null;
        if (request.ProductVariantId.HasValue)
        {
            variant = await _dbContext.ProductVariants.FirstOrDefaultAsync(x => x.Id == request.ProductVariantId && x.ProductId == request.ProductId);
            if (variant is null || !variant.IsActive || variant.Stock < request.Quantity) return false;
        }

        var existing = await _dbContext.CartItems.FirstOrDefaultAsync(x => x.UserId == userId && x.ProductId == request.ProductId && x.ProductVariantId == request.ProductVariantId);
        if (existing is null)
        {
            _dbContext.CartItems.Add(new CartItem { UserId = userId, ProductId = request.ProductId, ProductVariantId = request.ProductVariantId, Quantity = request.Quantity });
        }
        else
        {
            if (variant is null)
            {
                if (product.Stock < existing.Quantity + request.Quantity) return false;
            }
            else
            {
                if (variant.Stock < existing.Quantity + request.Quantity) return false;
            }
            existing.Quantity += request.Quantity;
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateItemAsync(Guid userId, Guid productId, UpdateCartItemRequest request)
    {
        var item = await _dbContext.CartItems.FirstOrDefaultAsync(x => x.UserId == userId && x.ProductId == productId);
        if (item is null) return false;
        if (request.Quantity <= 0)
        {
            _dbContext.CartItems.Remove(item);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        var product = await _dbContext.Products.FindAsync(productId);
        if (product is null || product.Stock < request.Quantity) return false;

        item.Quantity = request.Quantity;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveItemAsync(Guid userId, Guid productId)
    {
        var item = await _dbContext.CartItems.FirstOrDefaultAsync(x => x.UserId == userId && x.ProductId == productId);
        if (item is null) return false;
        _dbContext.CartItems.Remove(item);
        await _dbContext.SaveChangesAsync();
        return true;
    }
}

public class OrderService : IOrderService
{
    private readonly AppDbContext _dbContext;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IConfiguration _configuration;

    public OrderService(AppDbContext dbContext, IPaymentGateway paymentGateway, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _paymentGateway = paymentGateway;
        _configuration = configuration;
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

        order.Status = OrderStatus.Cancelled;
        AddOutbox("order.cancelled", new { orderId = order.Id, byAdmin = isAdmin });
        await _dbContext.SaveChangesAsync();
        return MapOrder(order);
    }

    public async Task<OrderDto?> UpdateStatusAsync(Guid orderId, OrderStatus status)
    {
        var order = await _dbContext.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == orderId);
        if (order is null) return null;

        if (!IsValidTransition(order.Status, status))
            throw new InvalidOperationException($"Invalid status transition: {order.Status} -> {status}");

        order.Status = status;
        AddOutbox("order.status_changed", new { orderId = order.Id, status = status.ToString() });
        await _dbContext.SaveChangesAsync();
        return MapOrder(order);
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

public class MockPaymentGateway : IPaymentGateway
{
    public Task<PaymentResult> ChargeAsync(Guid userId, decimal amount, string currency = "TRY")
    {
        if (amount <= 0)
            return Task.FromResult(new PaymentResult(false, null, "Invalid payment amount.", "mock"));

        var transactionId = $"MOCK-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32];
        return Task.FromResult(new PaymentResult(true, transactionId, null, "mock"));
    }

    public Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CheckoutSessionRequest request) =>
        Task.FromResult(new CheckoutSessionResult(false, null, null, "Mock provider does not use checkout sessions.", "mock"));
}

public class StripePaymentGateway : IPaymentGateway
{
    private readonly string _secretKey;

    public StripePaymentGateway(IConfiguration configuration)
    {
        _secretKey = configuration["Stripe:SecretKey"] ?? string.Empty;
    }

    public Task<PaymentResult> ChargeAsync(Guid userId, decimal amount, string currency = "TRY") =>
        Task.FromResult(new PaymentResult(false, null, "Use Stripe Checkout session flow.", "stripe"));

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CheckoutSessionRequest request)
    {
        if (request.Amount <= 0)
            return new CheckoutSessionResult(false, null, null, "Invalid payment amount.", "stripe");
        if (string.IsNullOrWhiteSpace(_secretKey))
            return new CheckoutSessionResult(false, null, null, "Stripe secret key is not configured.", "stripe");

        Stripe.StripeConfiguration.ApiKey = _secretKey;
        try
        {
            var lineItems = request.LineItems
                .Where(x => x.UnitAmountMinor != 0)
                .Select(x => new Stripe.Checkout.SessionLineItemOptions
                {
                    PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency.ToLowerInvariant(),
                        UnitAmount = Math.Abs(x.UnitAmountMinor),
                        ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                        {
                            Name = x.Name
                        }
                    },
                    Quantity = x.Quantity
                })
                .ToList();

            if (lineItems.Count == 0)
            {
                lineItems.Add(new Stripe.Checkout.SessionLineItemOptions
                {
                    PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency.ToLowerInvariant(),
                        UnitAmount = (long)Math.Round(request.Amount * 100m, MidpointRounding.AwayFromZero),
                        ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Order {request.OrderId}"
                        }
                    },
                    Quantity = 1
                });
            }

            var session = await new Stripe.Checkout.SessionService().CreateAsync(new Stripe.Checkout.SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = request.SuccessUrl,
                CancelUrl = request.CancelUrl,
                LineItems = lineItems,
                Metadata = new Dictionary<string, string>
                {
                    ["orderId"] = request.OrderId.ToString(),
                    ["userId"] = request.UserId.ToString()
                }
            });

            if (string.IsNullOrWhiteSpace(session.Url))
                return new CheckoutSessionResult(false, session.Id, null, "Stripe did not return a checkout URL.", "stripe");

            return new CheckoutSessionResult(true, session.Id, session.Url, null, "stripe");
        }
        catch (Stripe.StripeException ex)
        {
            var message = ex.StripeError?.Message ?? ex.Message;
            return new CheckoutSessionResult(false, null, null, message, "stripe");
        }
    }
}

public class AddressService : IAddressService
{
    private readonly AppDbContext _dbContext;

    public AddressService(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<List<AddressDto>> GetMyAddressesAsync(Guid userId) =>
        (await _dbContext.UserAddresses.Where(x => x.UserId == userId).OrderByDescending(x => x.IsDefault).ThenBy(x => x.Title).ToListAsync())
        .Select(Map).ToList();

    public async Task<AddressDto> CreateAsync(Guid userId, AddressRequest request)
    {
        if (request.IsDefault)
        {
            var defaults = await _dbContext.UserAddresses.Where(x => x.UserId == userId && x.IsDefault).ToListAsync();
            defaults.ForEach(x => x.IsDefault = false);
        }
        var entity = new UserAddress
        {
            UserId = userId,
            Title = request.Title,
            FullName = request.FullName,
            Phone = request.Phone,
            City = request.City,
            District = request.District,
            Line1 = request.Line1,
            Line2 = request.Line2,
            PostalCode = request.PostalCode,
            IsDefault = request.IsDefault
        };
        _dbContext.UserAddresses.Add(entity);
        await _dbContext.SaveChangesAsync();
        return Map(entity);
    }

    public async Task<AddressDto?> UpdateAsync(Guid userId, Guid id, AddressRequest request)
    {
        var entity = await _dbContext.UserAddresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (entity is null) return null;
        if (request.IsDefault)
        {
            var defaults = await _dbContext.UserAddresses.Where(x => x.UserId == userId && x.IsDefault && x.Id != id).ToListAsync();
            defaults.ForEach(x => x.IsDefault = false);
        }
        entity.Title = request.Title;
        entity.FullName = request.FullName;
        entity.Phone = request.Phone;
        entity.City = request.City;
        entity.District = request.District;
        entity.Line1 = request.Line1;
        entity.Line2 = request.Line2;
        entity.PostalCode = request.PostalCode;
        entity.IsDefault = request.IsDefault;
        await _dbContext.SaveChangesAsync();
        return Map(entity);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id)
    {
        var entity = await _dbContext.UserAddresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (entity is null) return false;
        _dbContext.UserAddresses.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    private static AddressDto Map(UserAddress x) => new(x.Id, x.Title, x.FullName, x.Phone, x.City, x.District, x.Line1, x.Line2, x.PostalCode, x.IsDefault);
}

public class CouponService : ICouponService
{
    private readonly AppDbContext _dbContext;

    public CouponService(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<List<CouponDto>> GetAllAsync() =>
        (await _dbContext.Coupons.OrderByDescending(x => x.IsActive).ThenBy(x => x.Code).ToListAsync()).Select(Map).ToList();

    public async Task<CouponDto?> GetByCodeAsync(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var coupon = await _dbContext.Coupons.FirstOrDefaultAsync(x => x.Code == normalized);
        return coupon is null ? null : Map(coupon);
    }

    public async Task<CouponDto> CreateAsync(CouponRequest request)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _dbContext.Coupons.AnyAsync(x => x.Code == code))
            throw new InvalidOperationException("Coupon code already exists.");
        var entity = new Coupon
        {
            Code = code,
            Percentage = request.Percentage,
            FixedAmount = request.FixedAmount,
            MinOrderAmount = request.MinOrderAmount,
            ExpiresAt = request.ExpiresAt,
            IsActive = request.IsActive
        };
        _dbContext.Coupons.Add(entity);
        await _dbContext.SaveChangesAsync();
        return Map(entity);
    }

    public async Task<CouponDto?> UpdateAsync(Guid id, CouponRequest request)
    {
        var entity = await _dbContext.Coupons.FindAsync(id);
        if (entity is null) return null;
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _dbContext.Coupons.AnyAsync(x => x.Id != id && x.Code == code))
            throw new InvalidOperationException("Coupon code already exists.");
        entity.Code = code;
        entity.Percentage = request.Percentage;
        entity.FixedAmount = request.FixedAmount;
        entity.MinOrderAmount = request.MinOrderAmount;
        entity.ExpiresAt = request.ExpiresAt;
        entity.IsActive = request.IsActive;
        await _dbContext.SaveChangesAsync();
        return Map(entity);
    }

    private static CouponDto Map(Coupon x) => new(x.Id, x.Code, x.Percentage, x.FixedAmount, x.MinOrderAmount, x.ExpiresAt, x.IsActive);
}

internal static class ProductServiceMap
{
    public static ProductDto MapProduct(Product product) => new(
        product.Id,
        product.Name,
        product.Price,
        product.Stock,
        product.IsActive,
        product.CategoryId,
        product.Category is null ? null : new CategoryDto(product.Category.Id, product.Category.Name, product.Category.Slug),
        product.Variants.Select(v => new ProductVariantDto(v.Id, v.Sku, v.Name, v.OverridePrice, v.Stock, v.IsActive)).ToList()
    );
}
