using Inventar.Storefront.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Storefront.Data;

public class StorefrontDbContext : DbContext
{
    public StorefrontDbContext(DbContextOptions<StorefrontDbContext> options)
        : base(options)
    {
    }

    public DbSet<StorefrontProduct> Products => Set<StorefrontProduct>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<WebOrder> WebOrders => Set<WebOrder>();
    public DbSet<WebOrderItem> WebOrderItems => Set<WebOrderItem>();
    public DbSet<WebOrderStatusHistory> WebOrderStatusHistory => Set<WebOrderStatusHistory>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<StorefrontProduct>(entity =>
        {
            entity.ToTable("Tepisi");
            entity.Property(x => x.Price).HasColumnType("decimal(18,2)");
            entity.Property(x => x.OnlinePrice).HasColumnType("decimal(18,2)");
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.Slug).IsUnique().HasFilter("[Slug] IS NOT NULL");
        });

        builder.Entity<ProductImage>(entity =>
        {
            entity.ToTable("ProductImages", "commerce");
            entity.Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne(x => x.Product)
                .WithMany(x => x.ProductImages)
                .HasForeignKey(x => x.TepihId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WebOrder>(entity =>
        {
            entity.ToTable("WebOrders", "commerce");
            entity.Property(x => x.ItemsTotal).HasColumnType("decimal(18,2)");
            entity.Property(x => x.ShippingTotal).HasColumnType("decimal(18,2)");
            entity.Property(x => x.DiscountTotal).HasColumnType("decimal(18,2)");
            entity.Property(x => x.GrandTotal).HasColumnType("decimal(18,2)");
            entity.Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.OrderNumber).IsUnique();
        });

        builder.Entity<WebOrderItem>(entity =>
        {
            entity.ToTable("WebOrderItems", "commerce");
            entity.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(x => x.LineTotal).HasColumnType("decimal(18,2)");
            entity.HasOne(x => x.WebOrder)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.WebOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.TepihId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<WebOrderStatusHistory>(entity =>
        {
            entity.ToTable("WebOrderStatusHistory", "commerce");
            entity.Property(x => x.ChangedUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne(x => x.WebOrder)
                .WithMany(x => x.StatusHistory)
                .HasForeignKey(x => x.WebOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InventoryReservation>(entity =>
        {
            entity.ToTable("InventoryReservations", "commerce");
            entity.Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne(x => x.WebOrder)
                .WithMany(x => x.Reservations)
                .HasForeignKey(x => x.WebOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.TepihId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
