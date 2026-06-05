using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using ShopAPI.Application;
using ShopAPI.Domain;

namespace ShopAPI.Infrastructure;

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
