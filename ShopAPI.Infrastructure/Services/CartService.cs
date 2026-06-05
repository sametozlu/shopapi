using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using ShopAPI.Application;
using ShopAPI.Domain;

namespace ShopAPI.Infrastructure;

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
