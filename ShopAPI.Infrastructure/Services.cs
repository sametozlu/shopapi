using Microsoft.EntityFrameworkCore;
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

    public Task<List<Category>> GetAllAsync() => _dbContext.Categories.OrderBy(x => x.Name).ToListAsync();

    public async Task<Category> CreateAsync(CategoryRequest request)
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
        return category;
    }

    public async Task<Category?> UpdateAsync(Guid id, CategoryRequest request)
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
        return category;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var category = await _dbContext.Categories.FindAsync(id);
        if (category is null) return false;
        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync();
        return true;
    }
}

public class ProductService : IProductService
{
    private readonly AppDbContext _dbContext;

    public ProductService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<Product>> GetAllAsync(ProductQuery query)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);
        var q = _dbContext.Products.Include(x => x.Category).AsQueryable();
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
        return new PagedResult<Product>(items, page, pageSize, totalCount);
    }

    public Task<Product?> GetByIdAsync(Guid id) => _dbContext.Products.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);

    public async Task<Product?> CreateAsync(ProductRequest request)
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

    public async Task<Product?> UpdateAsync(Guid id, ProductRequest request)
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
}

public class CartService : ICartService
{
    private readonly AppDbContext _dbContext;

    public CartService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<CartItem>> GetMyCartAsync(Guid userId) =>
        _dbContext.CartItems
            .Include(x => x.Product)
            .ThenInclude(x => x!.Category)
            .Where(x => x.UserId == userId)
            .ToListAsync();

    public async Task<bool> AddItemAsync(Guid userId, AddCartItemRequest request)
    {
        if (request.Quantity <= 0) throw new ArgumentException("Quantity must be greater than 0.");
        var product = await _dbContext.Products.FindAsync(request.ProductId);
        if (product is null || !product.IsActive || product.Stock < request.Quantity) return false;

        var existing = await _dbContext.CartItems.FirstOrDefaultAsync(x => x.UserId == userId && x.ProductId == request.ProductId);
        if (existing is null)
        {
            _dbContext.CartItems.Add(new CartItem { UserId = userId, ProductId = request.ProductId, Quantity = request.Quantity });
        }
        else
        {
            if (product.Stock < existing.Quantity + request.Quantity) return false;
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

    public OrderService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Order?> CreateOrderAsync(Guid userId)
    {
        var cartItems = await _dbContext.CartItems.Include(x => x.Product).Where(x => x.UserId == userId).ToListAsync();
        if (!cartItems.Any()) return null;

        foreach (var cartItem in cartItems)
        {
            if (cartItem.Product is null || cartItem.Product.Stock < cartItem.Quantity) return null;
        }

        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.Paid
        };

        foreach (var cartItem in cartItems)
        {
            var product = cartItem.Product!;
            product.Stock -= cartItem.Quantity;
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = cartItem.Quantity
            });
        }

        order.TotalAmount = order.Items.Sum(x => x.UnitPrice * x.Quantity);
        _dbContext.Orders.Add(order);
        _dbContext.CartItems.RemoveRange(cartItems);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == order.Id);
    }

    public Task<List<Order>> GetMyOrdersAsync(Guid userId) =>
        _dbContext.Orders.Include(x => x.Items).Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).ToListAsync();

    public async Task<Order?> UpdateStatusAsync(Guid orderId, OrderStatus status)
    {
        var order = await _dbContext.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == orderId);
        if (order is null) return null;
        order.Status = status;
        await _dbContext.SaveChangesAsync();
        return order;
    }
}
