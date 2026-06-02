using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShopAPI.Domain;

namespace ShopAPI.Infrastructure.Persistence;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is AppDbContext dbContext)
        {
            var entries = dbContext.ChangeTracker.Entries().ToList();
            foreach (var entry in entries)
            {
                if (entry.Entity is AuditLog or RefreshToken) continue;
                if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                {
                    dbContext.AuditLogs.Add(new AuditLog
                    {
                        EntityName = entry.Entity.GetType().Name,
                        Action = entry.State.ToString(),
                        Details = string.Join("; ", entry.Properties
                            .Where(p => p.IsModified || entry.State == EntityState.Added)
                            .Select(p => $"{p.Metadata.Name}={p.CurrentValue}")
                            .Take(8))
                    });
                }
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
