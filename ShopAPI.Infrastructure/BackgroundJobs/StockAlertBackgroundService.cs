using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ShopAPI.Infrastructure.BackgroundJobs;

public class StockAlertBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StockAlertBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    public StockAlertBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<StockAlertBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _configuration.GetValue("StockAlert:IntervalMinutes", 5);
        var threshold = _configuration.GetValue("StockAlert:Threshold", 10);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var lowStock = await db.Products
                    .Where(p => p.IsActive && p.Stock <= threshold)
                    .Select(p => new { p.Name, p.Stock })
                    .ToListAsync(stoppingToken);

                foreach (var item in lowStock)
                {
                    _logger.LogWarning(
                        "STOCK ALERT: Product {ProductName} has low stock ({Stock}, threshold {Threshold})",
                        item.Name, item.Stock, threshold);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stock alert job failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }
}
