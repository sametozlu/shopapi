using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using ShopAPI.Application;
using ShopAPI.Domain;

namespace ShopAPI.Infrastructure;

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
