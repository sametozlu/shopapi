using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using ShopAPI.Application;
using ShopAPI.Domain;

namespace ShopAPI.Infrastructure;

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
