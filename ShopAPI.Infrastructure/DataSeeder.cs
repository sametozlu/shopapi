using Microsoft.EntityFrameworkCore;
using ShopAPI.Domain;

namespace ShopAPI.Infrastructure;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext dbContext)
    {
        if (await dbContext.Categories.AnyAsync()) return;

        var electronics = new Category { Name = "Electronics", Slug = "electronics" };
        var gaming = new Category { Name = "Gaming", Slug = "gaming" };
        var home = new Category { Name = "Home", Slug = "home" };

        dbContext.Categories.AddRange(electronics, gaming, home);
        await dbContext.SaveChangesAsync();

        dbContext.Products.AddRange(
            new Product { Name = "Wireless Mouse", Price = 599, Stock = 120, CategoryId = electronics.Id, IsActive = true },
            new Product { Name = "Mechanical Keyboard", Price = 1899, Stock = 75, CategoryId = electronics.Id, IsActive = true },
            new Product { Name = "Gaming Headset", Price = 2499, Stock = 40, CategoryId = gaming.Id, IsActive = true },
            new Product { Name = "Desk Lamp", Price = 399, Stock = 150, CategoryId = home.Id, IsActive = true }
        );

        if (!await dbContext.Users.AnyAsync(x => x.Email == "admin@admin.local"))
        {
            dbContext.Users.Add(new AppUser
            {
                FullName = "System Admin",
                Email = "admin@admin.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                Role = UserRole.Admin
            });
        }

        await dbContext.SaveChangesAsync();
    }
}
