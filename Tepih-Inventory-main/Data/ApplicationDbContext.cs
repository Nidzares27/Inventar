using Inventar.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Data
{
    public class ApplicationDbContext : IdentityDbContext<AppUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : base(options)
        {
        }
        public DbSet<Tepih> Tepisi { get; set; }
        public DbSet<Prodaja> Prodaje { get; set; }
        public DbSet<Kupac> Kupci { get; set; }
        public DbSet<Placanje> Placanja { get; set; }
        public DbSet<Dug> Dugovanja { get; set; }

        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<WebOrder> WebOrders { get; set; }
        public DbSet<WebOrderItem> WebOrderItems { get; set; }
        public DbSet<WebOrderStatusHistory> WebOrderStatusHistory { get; set; }
        public DbSet<InventoryReservation> InventoryReservations { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Tepih>(entity =>
            {
                entity.Property(x => x.Price).HasColumnType("decimal(18,2)");
                entity.Property(x => x.OnlinePrice).HasColumnType("decimal(18,2)");
                entity.Property(x => x.RowVersion).IsRowVersion();
                entity.HasIndex(x => x.Slug).IsUnique().HasFilter("[Slug] IS NOT NULL");
                entity.HasIndex(x => x.UnID).IsUnique().HasFilter("[UnID] IS NOT NULL");
            });

            builder.Entity<ProductImage>(entity =>
            {
                entity.ToTable("ProductImages", "commerce");
                entity.Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(x => x.MediaType).HasMaxLength(20).HasDefaultValue("image");
                entity.HasIndex(x => new { x.TepihId, x.SortOrder });
                entity.HasIndex(x => new { x.TepihId, x.IsPrimary });
                entity.HasOne(x => x.Tepih)
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
                entity.HasIndex(x => new { x.Status, x.CreatedUtc });
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
                entity.HasOne(x => x.Tepih)
                    .WithMany(x => x.WebOrderItems)
                    .HasForeignKey(x => x.TepihId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<WebOrderStatusHistory>(entity =>
            {
                entity.ToTable("WebOrderStatusHistory", "commerce");
                entity.Property(x => x.ChangedUtc).HasDefaultValueSql("GETUTCDATE()");
                entity.HasIndex(x => new { x.WebOrderId, x.ChangedUtc });
                entity.HasOne(x => x.WebOrder)
                    .WithMany(x => x.StatusHistory)
                    .HasForeignKey(x => x.WebOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<InventoryReservation>(entity =>
            {
                entity.ToTable("InventoryReservations", "commerce");
                entity.Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
                entity.HasIndex(x => new { x.TepihId, x.Status });
                entity.HasIndex(x => new { x.WebOrderId, x.Status });
                entity.HasOne(x => x.WebOrder)
                    .WithMany(x => x.Reservations)
                    .HasForeignKey(x => x.WebOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.WebOrderItem)
                    .WithMany(x => x.Reservations)
                    .HasForeignKey(x => x.WebOrderItemId)
                    .OnDelete(DeleteBehavior.NoAction);
                entity.HasOne(x => x.Tepih)
                    .WithMany(x => x.InventoryReservations)
                    .HasForeignKey(x => x.TepihId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Prodaja>(entity =>
            {
                entity.Property(x => x.Price).HasColumnType("decimal(18,4)");
                entity.Property(x => x.DirectSaleOriginalTotal).HasColumnType("decimal(18,2)");
            });

            builder.Entity<Dug>(entity =>
            {
                entity.Property(x => x.DebtAmount).HasColumnType("decimal(18,2)");
            });

            builder.Entity<Kupac>(entity =>
            {
                entity.Property(x => x.LeftToPay).HasColumnType("decimal(18,2)");
            });

            builder.Entity<Placanje>(entity =>
            {
                entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            });
        }
    }
}
