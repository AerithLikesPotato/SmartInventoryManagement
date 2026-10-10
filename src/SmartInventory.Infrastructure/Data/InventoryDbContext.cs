using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.Entities;
using SmartInventory.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace SmartInventory.Infrastructure.Data;

public class InventoryDbContext : IdentityDbContext<ApplicationUser>
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<InventoryAlert> InventoryAlerts => Set<InventoryAlert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.Sku).IsUnique();
            entity.Property(p => p.Sku).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.UnitPrice).HasPrecision(18, 2);
            entity.Property(p => p.ImageUrl).HasMaxLength(500);
        });
        modelBuilder.Entity<StockTransaction>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => new { t.ProductId, t.TimestampUtc });
            entity.Property(t => t.PerformedBy).HasMaxLength(256);
            entity.HasOne(t => t.Product)
                .WithMany(p => p.Transactions)
                .HasForeignKey(t => t.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<InventoryAlert>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.ProductId, a.IsResolved });
            entity.Property(a => a.AlertType).HasMaxLength(50).IsRequired();

            entity.HasOne(a => a.Product)
                .WithMany()
                .HasForeignKey(a => a.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}