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

public record AddCartItemRequest(Guid ProductId, int Quantity);
public record UpdateCartItemRequest(int Quantity);

public record CreateOrderRequest();

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RefreshAsync(RefreshTokenRequest request);
    Task RevokeAsync(RefreshTokenRequest request);
}

public interface ICategoryService
{
    Task<List<Category>> GetAllAsync();
    Task<Category> CreateAsync(CategoryRequest request);
    Task<Category?> UpdateAsync(Guid id, CategoryRequest request);
    Task<bool> DeleteAsync(Guid id);
}

public interface IProductService
{
    Task<PagedResult<Product>> GetAllAsync(ProductQuery query);
    Task<Product?> GetByIdAsync(Guid id);
    Task<Product?> CreateAsync(ProductRequest request);
    Task<Product?> UpdateAsync(Guid id, ProductRequest request);
    Task<bool> DeleteAsync(Guid id);
}

public interface ICartService
{
    Task<List<CartItem>> GetMyCartAsync(Guid userId);
    Task<bool> AddItemAsync(Guid userId, AddCartItemRequest request);
    Task<bool> UpdateItemAsync(Guid userId, Guid productId, UpdateCartItemRequest request);
    Task<bool> RemoveItemAsync(Guid userId, Guid productId);
}

public interface IOrderService
{
    Task<Order?> CreateOrderAsync(Guid userId);
    Task<List<Order>> GetMyOrdersAsync(Guid userId);
    Task<Order?> UpdateStatusAsync(Guid orderId, OrderStatus status);
}
