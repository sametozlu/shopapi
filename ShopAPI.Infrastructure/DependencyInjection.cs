using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopAPI.Application;
using ShopAPI.Infrastructure.Auth;
using ShopAPI.Infrastructure.BackgroundJobs;
using ShopAPI.Infrastructure.Caching;
using ShopAPI.Infrastructure.Persistence;

namespace ShopAPI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=shopapi.db";

        services.AddSingleton<AuditSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
                options.UseSqlite(connectionString);
            else
                options.UseSqlServer(connectionString);

            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        var redisEnabled = configuration.GetValue("Redis:Enabled", false);
        var redisConnection = configuration["Redis:ConnectionString"];
        if (redisEnabled && !string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<IAuthService>(sp => sp.GetRequiredService<AuthService>());

        services.AddScoped<CategoryService>();
        services.AddScoped<ICategoryService>(sp => sp.GetRequiredService<CategoryService>());

        services.AddScoped<ProductService>();
        services.AddScoped<IProductService, ProductCacheService>();

        services.AddScoped<CartService>();
        services.AddScoped<ICartService>(sp => sp.GetRequiredService<CartService>());

        services.AddScoped<OrderService>();
        services.AddScoped<IOrderService>(sp => sp.GetRequiredService<OrderService>());

        services.AddHostedService<StockAlertBackgroundService>();

        return services;
    }
}
