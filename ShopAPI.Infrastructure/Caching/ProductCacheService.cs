using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ShopAPI.Application;
using ShopAPI.Domain;

namespace ShopAPI.Infrastructure.Caching;

public class ProductCacheService : IProductService
{
    private readonly ProductService _inner;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ProductCacheService> _logger;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    public ProductCacheService(ProductService inner, IDistributedCache cache, ILogger<ProductCacheService> logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PagedResult<Product>> GetAllAsync(ProductQuery query)
    {
        var key = $"products:{query.Search}:{query.CategoryId}:{query.MinPrice}:{query.MaxPrice}:{query.IsActive}:{query.SortBy}:{query.Desc}:{query.Page}:{query.PageSize}";
        var cached = await _cache.GetStringAsync(key);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for {Key}", key);
            return JsonSerializer.Deserialize<PagedResult<Product>>(cached)!;
        }

        var result = await _inner.GetAllAsync(query);
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(result), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration
        });
        return result;
    }

    public async Task<Product?> GetByIdAsync(Guid id)
    {
        var key = $"product:{id}";
        var cached = await _cache.GetStringAsync(key);
        if (cached is not null)
            return JsonSerializer.Deserialize<Product>(cached);

        var product = await _inner.GetByIdAsync(id);
        if (product is not null)
        {
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(product), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration
            });
        }
        return product;
    }

    public Task<Product?> CreateAsync(ProductRequest request) => _inner.CreateAsync(request);
    public Task<Product?> UpdateAsync(Guid id, ProductRequest request) => _inner.UpdateAsync(id, request);
    public Task<bool> DeleteAsync(Guid id) => _inner.DeleteAsync(id);
}
