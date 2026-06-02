using Microsoft.EntityFrameworkCore;
using ShopAPI.Domain;

namespace ShopAPI.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<Category>().HasIndex(x => x.Slug).IsUnique();

        modelBuilder.Entity<CartItem>()
            .HasIndex(x => new { x.UserId, x.ProductId, x.ProductVariantId })
            .IsUnique();

        modelBuilder.Entity<CartItem>()
            .HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OrderItem>()
            .HasOne(x => x.Order)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.OrderId);

        modelBuilder.Entity<OrderItem>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OrderItem>()
            .HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Order>()
            .HasOne(x => x.ShippingAddress)
            .WithMany()
            .HasForeignKey(x => x.ShippingAddressId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Product>()
            .Property(x => x.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Order>()
            .Property(x => x.TotalAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Order>()
            .Property(x => x.ShippingCost)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Order>()
            .Property(x => x.DiscountAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<OrderItem>()
            .Property(x => x.UnitPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ProductVariant>()
            .Property(x => x.OverridePrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<UserAddress>()
            .HasIndex(x => new { x.UserId, x.IsDefault });

        modelBuilder.Entity<Coupon>()
            .HasIndex(x => x.Code)
            .IsUnique();

        modelBuilder.Entity<Coupon>()
            .Property(x => x.Percentage)
            .HasPrecision(5, 2);

        modelBuilder.Entity<Coupon>()
            .Property(x => x.FixedAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Coupon>()
            .Property(x => x.MinOrderAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ProductVariant>()
            .HasIndex(x => x.Sku)
            .IsUnique();

        modelBuilder.Entity<PaymentTransaction>()
            .Property(x => x.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PaymentTransaction>()
            .HasIndex(x => x.ProviderTransactionId)
            .IsUnique();

        modelBuilder.Entity<OutboxEvent>()
            .HasIndex(x => new { x.ProcessedAt, x.CreatedAt });

        modelBuilder.Entity<RefreshToken>().HasIndex(x => x.TokenHash);
    }
}
