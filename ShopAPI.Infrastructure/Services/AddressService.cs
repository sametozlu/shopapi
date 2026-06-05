using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using ShopAPI.Application;
using ShopAPI.Domain;

namespace ShopAPI.Infrastructure;

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
