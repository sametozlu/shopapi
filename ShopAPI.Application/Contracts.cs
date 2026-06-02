using ShopAPI.Domain;

namespace ShopAPI.Application;

public record RegisterRequest(string FullName, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);
public record AuthResponse(string Token, string RefreshToken, string Email, string Role, DateTime ExpiresAt);

public interface ITokenService
{
    AuthResponse IssueTokens(AppUser user);
    Task<AuthResponse?> RefreshAsync(string refreshToken);
    Task RevokeRefreshTokenAsync(string refreshToken);
}
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record CategoryRequest(string Name, string Slug);
public record ProductRequest(string Name, decimal Price, int Stock, Guid CategoryId, bool IsActive);
public record ProductVariantRequest(string Sku, string Name, decimal? OverridePrice, int Stock, bool IsActive);
public record ProductQuery(
    string? Search,
    Guid? CategoryId,
    decimal? MinPrice,
    decimal? MaxPrice,
    bool? IsActive,
    string? SortBy,
    bool Desc,
    int Page,
    int PageSize);

public record AddCartItemRequest(Guid ProductId, Guid? ProductVariantId, int Quantity);
public record UpdateCartItemRequest(int Quantity);

public record AddressRequest(
    string Title,
    string FullName,
    string Phone,
    string City,
    string District,
    string Line1,
    string? Line2,
    string PostalCode,
    bool IsDefault);
public record CouponRequest(string Code, decimal? Percentage, decimal? FixedAmount, decimal MinOrderAmount, DateTime ExpiresAt, bool IsActive);
public record CreateOrderRequest(Guid ShippingAddressId, ShippingMethod ShippingMethod, string? CouponCode);
public record PaymentResult(bool Success, string? TransactionId, string? ErrorMessage, string Provider = "mock");
public record CheckoutLineItem(string Name, long UnitAmountMinor, int Quantity);
public record CheckoutSessionRequest(
    Guid OrderId,
    Guid UserId,
    decimal Amount,
    string Currency,
    IReadOnlyList<CheckoutLineItem> LineItems,
    string SuccessUrl,
    string CancelUrl);
public record CheckoutSessionResult(bool Success, string? SessionId, string? Url, string? ErrorMessage, string Provider = "stripe");
public record PaymentStartResponse(string Mode, OrderDto? Order, string? CheckoutUrl, string? SessionId);
public record PaymentWebhookRequest(string ProviderTransactionId, string Status, string? FailureReason);

public record CategoryDto(Guid Id, string Name, string Slug);
public record ProductVariantDto(Guid Id, string Sku, string Name, decimal? OverridePrice, int Stock, bool IsActive);
public record ProductDto(Guid Id, string Name, decimal Price, int Stock, bool IsActive, Guid CategoryId, CategoryDto? Category, IReadOnlyList<ProductVariantDto> Variants);
public record CartItemDto(Guid Id, Guid ProductId, Guid? ProductVariantId, int Quantity, ProductDto? Product, ProductVariantDto? ProductVariant);
public record OrderItemDto(Guid Id, Guid ProductId, Guid? ProductVariantId, string ProductName, string? VariantName, decimal UnitPrice, int Quantity);
public record AddressDto(Guid Id, string Title, string FullName, string Phone, string City, string District, string Line1, string? Line2, string PostalCode, bool IsDefault);
public record CouponDto(Guid Id, string Code, decimal? Percentage, decimal? FixedAmount, decimal MinOrderAmount, DateTime ExpiresAt, bool IsActive);
public record OrderDto(
    Guid Id,
    Guid UserId,
    decimal TotalAmount,
    decimal ShippingCost,
    decimal DiscountAmount,
    string? CouponCode,
    ShippingMethod ShippingMethod,
    Guid? ShippingAddressId,
    OrderStatus Status,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemDto> Items);

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RefreshAsync(RefreshTokenRequest request);
    Task RevokeAsync(RefreshTokenRequest request);
}

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync();
    Task<CategoryDto> CreateAsync(CategoryRequest request);
    Task<CategoryDto?> UpdateAsync(Guid id, CategoryRequest request);
    Task<bool> DeleteAsync(Guid id);
}

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetAllAsync(ProductQuery query);
    Task<ProductDto?> GetByIdAsync(Guid id);
    Task<ProductDto?> CreateAsync(ProductRequest request);
    Task<ProductDto?> UpdateAsync(Guid id, ProductRequest request);
    Task<bool> DeleteAsync(Guid id);
    Task<List<ProductVariantDto>> GetVariantsAsync(Guid productId);
    Task<ProductVariantDto?> CreateVariantAsync(Guid productId, ProductVariantRequest request);
    Task<ProductVariantDto?> UpdateVariantAsync(Guid productId, Guid variantId, ProductVariantRequest request);
}

public interface ICartService
{
    Task<List<CartItemDto>> GetMyCartAsync(Guid userId);
    Task<bool> AddItemAsync(Guid userId, AddCartItemRequest request);
    Task<bool> UpdateItemAsync(Guid userId, Guid productId, UpdateCartItemRequest request);
    Task<bool> RemoveItemAsync(Guid userId, Guid productId);
}

public interface IOrderService
{
    Task<OrderDto?> CreateOrderAsync(Guid userId, CreateOrderRequest request);
    Task<List<OrderDto>> GetMyOrdersAsync(Guid userId);
    Task<List<OrderDto>> GetAllOrdersAsync();
    Task<PaymentStartResponse?> PayOrderAsync(Guid userId, Guid orderId);
    Task<OrderDto?> ConfirmPaymentAsync(Guid userId, Guid orderId, string sessionId);
    Task<OrderDto?> HandlePaymentWebhookAsync(PaymentWebhookRequest request);
    Task<OrderDto?> CancelOrderAsync(Guid userId, Guid orderId, bool isAdmin);
    Task<OrderDto?> UpdateStatusAsync(Guid orderId, OrderStatus status);
}

public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(Guid userId, decimal amount, string currency = "TRY");
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CheckoutSessionRequest request);
}

public interface IAddressService
{
    Task<List<AddressDto>> GetMyAddressesAsync(Guid userId);
    Task<AddressDto> CreateAsync(Guid userId, AddressRequest request);
    Task<AddressDto?> UpdateAsync(Guid userId, Guid id, AddressRequest request);
    Task<bool> DeleteAsync(Guid userId, Guid id);
}

public interface ICouponService
{
    Task<List<CouponDto>> GetAllAsync();
    Task<CouponDto?> GetByCodeAsync(string code);
    Task<CouponDto> CreateAsync(CouponRequest request);
    Task<CouponDto?> UpdateAsync(Guid id, CouponRequest request);
}
