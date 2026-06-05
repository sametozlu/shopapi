using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShopAPI.Infrastructure;

namespace ShopAPI.Tests.Integration;

public class ShopApiFactory : WebApplicationFactory<Program>
{
    private DbConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", "integration-test-secret-key-min-32-chars-long");
        builder.UseSetting("Jwt:Issuer", "ShopAPI");
        builder.UseSetting("Jwt:Audience", "ShopAPI.Client");
        builder.UseSetting("Payments:Provider", "mock");
        builder.UseSetting("Payments:WebhookSecret", "test-webhook-secret");
        builder.UseSetting("Redis:Enabled", "false");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "integration-test-secret-key-min-32-chars-long",
                ["Jwt:Issuer"] = "ShopAPI",
                ["Jwt:Audience"] = "ShopAPI.Client",
                ["Payments:Provider"] = "mock",
                ["Payments:WebhookSecret"] = "test-webhook-secret",
                ["Redis:Enabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptors = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(AppDbContext))
                .ToList();
            foreach (var descriptor in descriptors)
                services.Remove(descriptor);

            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            var hosted = services
                .Where(d => typeof(IHostedService).IsAssignableFrom(d.ServiceType))
                .ToList();
            foreach (var descriptor in hosted)
                services.Remove(descriptor);
        });
    }

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await DataSeeder.SeedAsync(db);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _connection?.Dispose();
        base.Dispose(disposing);
    }
}
