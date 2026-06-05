using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using ShopAPI.Application;
using ShopAPI.Domain;

namespace ShopAPI.Infrastructure;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _dbContext;

    public CategoryService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CategoryDto>> GetAllAsync() =>
        (await _dbContext.Categories.OrderBy(x => x.Name).ToListAsync()).Select(MapCategory).ToList();

    public async Task<CategoryDto> CreateAsync(CategoryRequest request)
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
        return MapCategory(category);
    }

    public async Task<CategoryDto?> UpdateAsync(Guid id, CategoryRequest request)
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
        return MapCategory(category);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var category = await _dbContext.Categories.FindAsync(id);
        if (category is null) return false;
        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    private static CategoryDto MapCategory(Category category) => new(category.Id, category.Name, category.Slug);
}
