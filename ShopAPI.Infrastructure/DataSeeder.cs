using Microsoft.EntityFrameworkCore;
using ShopAPI.Domain;

namespace ShopAPI.Infrastructure;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext dbContext)
    {
        var categoryMap = await dbContext.Categories.ToDictionaryAsync(c => c.Slug, c => c);
        foreach (var (name, slug) in new[]
        {
            ("Electronics", "electronics"),
            ("Gaming", "gaming"),
            ("Home", "home")
        })
        {
            if (!categoryMap.ContainsKey(slug))
            {
                var category = new Category { Name = name, Slug = slug };
                dbContext.Categories.Add(category);
                categoryMap[slug] = category;
            }
        }

        await dbContext.SaveChangesAsync();

        var existingProductNames = await dbContext.Products.Select(p => p.Name).ToListAsync();
        var existingSet = existingProductNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var products = new (string Name, decimal Price, int Stock, string CategorySlug)[]
        {
            ("Wireless Mouse", 599, 120, "electronics"),
            ("Mechanical Keyboard", 1899, 75, "electronics"),
            ("Gaming Headset", 2499, 40, "gaming"),
            ("Desk Lamp", 399, 150, "home"),
            ("27\" 4K Monitor", 10499, 18, "electronics"),
            ("USB-C Hub", 1199, 64, "electronics"),
            ("Portable SSD 1TB", 3299, 42, "electronics"),
            ("Webcam Full HD", 1499, 33, "electronics"),
            ("Bluetooth Speaker", 2299, 28, "electronics"),
            ("Noise Cancelling Earbuds", 3899, 24, "electronics"),
            ("Laptop Stand", 799, 90, "electronics"),
            ("Gaming Mousepad", 499, 110, "gaming"),
            ("Mechanical Number Pad", 999, 52, "gaming"),
            ("Ergonomic Chair", 8499, 16, "home"),
            ("Coffee Maker", 3199, 22, "home"),
            ("Air Fryer", 4599, 19, "home"),
            ("Smart Bulb", 349, 130, "home"),
            ("Office Desk", 6299, 14, "home"),
            ("Robot Vacuum", 12499, 11, "home"),
            ("Smart Plug", 299, 160, "home")
        };

        foreach (var item in products)
        {
            if (existingSet.Contains(item.Name)) continue;
            dbContext.Products.Add(new Product
            {
                Name = item.Name,
                Price = item.Price,
                Stock = item.Stock,
                CategoryId = categoryMap[item.CategorySlug].Id,
                IsActive = true
            });
        }

        await dbContext.SaveChangesAsync();

        var allProducts = await dbContext.Products.ToListAsync();
        var variantSkus = await dbContext.ProductVariants.Select(x => x.Sku).ToListAsync();
        var skuSet = variantSkus.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var product in allProducts.Take(8))
        {
            var skuA = $"{product.Name[..Math.Min(3, product.Name.Length)].ToUpperInvariant()}-STD";
            var skuB = $"{product.Name[..Math.Min(3, product.Name.Length)].ToUpperInvariant()}-PRO";
            if (!skuSet.Contains(skuA))
            {
                dbContext.ProductVariants.Add(new ProductVariant
                {
                    ProductId = product.Id,
                    Sku = skuA,
                    Name = "Standard",
                    OverridePrice = null,
                    Stock = Math.Max(5, product.Stock / 2),
                    IsActive = true
                });
            }
            if (!skuSet.Contains(skuB))
            {
                dbContext.ProductVariants.Add(new ProductVariant
                {
                    ProductId = product.Id,
                    Sku = skuB,
                    Name = "Pro",
                    OverridePrice = product.Price + 200,
                    Stock = Math.Max(3, product.Stock / 3),
                    IsActive = true
                });
            }
        }

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

        var adminUser = await dbContext.Users.FirstOrDefaultAsync(x => x.Email == "admin@admin.local");
        if (adminUser is not null && !await dbContext.UserAddresses.AnyAsync(x => x.UserId == adminUser.Id))
        {
            dbContext.UserAddresses.Add(new UserAddress
            {
                UserId = adminUser.Id,
                Title = "Ev",
                FullName = "System Admin",
                Phone = "05550000000",
                City = "Istanbul",
                District = "Kadikoy",
                Line1 = "Demo Mah. Demo Sok. No:1",
                PostalCode = "34710",
                IsDefault = true
            });
        }

        if (!await dbContext.Coupons.AnyAsync(x => x.Code == "WELCOME10"))
        {
            dbContext.Coupons.Add(new Coupon
            {
                Code = "WELCOME10",
                Percentage = 10,
                FixedAmount = null,
                MinOrderAmount = 1000,
                ExpiresAt = DateTime.UtcNow.AddMonths(3),
                IsActive = true
            });
        }

        await dbContext.SaveChangesAsync();
    }
}
