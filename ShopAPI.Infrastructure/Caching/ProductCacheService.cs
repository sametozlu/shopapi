using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ShopAPI.Application;

namespace ShopAPI.Infrastructure.Caching;

public class ProductCacheService : IProductService
{
    private readonly ProductService _inner;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ProductCacheService> _logger;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);
    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public ProductCacheService(ProductService inner, IDistributedCache cache, ILogger<ProductCacheService> logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PagedResult<ProductDto>> GetAllAsync(ProductQuery query)
    {
        var key = $"products:{query.Search}:{query.CategoryId}:{query.MinPrice}:{query.MaxPrice}:{query.IsActive}:{query.SortBy}:{query.Desc}:{query.Page}:{query.PageSize}";
        var cached = await _cache.GetStringAsync(key);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for {Key}", key);
            return JsonSerializer.Deserialize<PagedResult<ProductDto>>(cached, CacheJsonOptions)!;
        }

        var result = await _inner.GetAllAsync(query);
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(result, CacheJsonOptions), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration
        });
        return result;
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id)
    {
        var key = $"product:{id}";
        var cached = await _cache.GetStringAsync(key);
        if (cached is not null)
            return JsonSerializer.Deserialize<ProductDto>(cached, CacheJsonOptions);

        var product = await _inner.GetByIdAsync(id);
        if (product is not null)
        {
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(product, CacheJsonOptions), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration
            });
        }
        return product;
    }

    public Task<ProductDto?> CreateAsync(ProductRequest request) => _inner.CreateAsync(request);
    public Task<ProductDto?> UpdateAsync(Guid id, ProductRequest request) => _inner.UpdateAsync(id, request);
    public Task<bool> DeleteAsync(Guid id) => _inner.DeleteAsync(id);
    public Task<List<ProductVariantDto>> GetVariantsAsync(Guid productId) => _inner.GetVariantsAsync(productId);
    public Task<ProductVariantDto?> CreateVariantAsync(Guid productId, ProductVariantRequest request) => _inner.CreateVariantAsync(productId, request);
    public Task<ProductVariantDto?> UpdateVariantAsync(Guid productId, Guid variantId, ProductVariantRequest request) => _inner.UpdateVariantAsync(productId, variantId, request);
}
