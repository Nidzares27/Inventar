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
    public DbSet<StorefrontSale> Sales => Set<StorefrontSale>();
    public DbSet<WebOrder> WebOrders => Set<WebOrder>();
    public DbSet<WebOrderItem> WebOrderItems => Set<WebOrderItem>();
    public DbSet<WebOrderStatusHistory> WebOrderStatusHistory => Set<WebOrderStatusHistory>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();
    public DbSet<StorefrontCustomer> StorefrontCustomers => Set<StorefrontCustomer>();
    public DbSet<StorefrontLoginCode> StorefrontLoginCodes => Set<StorefrontLoginCode>();

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
            entity.Property(x => x.MediaType).HasMaxLength(20).HasDefaultValue("image");
            entity.HasOne(x => x.Product)
                .WithMany(x => x.ProductImages)
                .HasForeignKey(x => x.TepihId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StorefrontSale>(entity =>
        {
            entity.ToTable("Prodaje");
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
            entity.HasOne(x => x.StorefrontCustomer)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.StorefrontCustomerId)
                .OnDelete(DeleteBehavior.SetNull);
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
            entity.HasOne(x => x.WebOrderItem)
                .WithMany(x => x.Reservations)
                .HasForeignKey(x => x.WebOrderItemId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.TepihId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StorefrontCustomer>(entity =>
        {
            entity.ToTable("StorefrontCustomers", "commerce");
            entity.Property(x => x.Email).IsRequired();
            entity.Property(x => x.NormalizedEmail).IsRequired();
            entity.Property(x => x.Country).HasDefaultValue("Crna Gora");
            entity.Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(x => x.NormalizedEmail).IsUnique();
        });

        builder.Entity<StorefrontLoginCode>(entity =>
        {
            entity.ToTable("StorefrontLoginCodes", "commerce");
            entity.Property(x => x.Email).IsRequired();
            entity.Property(x => x.NormalizedEmail).IsRequired();
            entity.Property(x => x.Purpose).IsRequired();
            entity.Property(x => x.CodeHash).IsRequired();
            entity.Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(x => new { x.NormalizedEmail, x.Purpose, x.UsedUtc, x.ExpiresUtc });
        });
    }
}
