# E-Commerce Schema And Migration Plan

## Why this shape fits the current app

Your current inventory app uses:

- `Tepih` as the product + stock table
- `Prodaja` as a completed sale record
- `ApplicationDbContext` as the single EF Core context

Relevant current files:

- `Tepih` only has a single stock column today: `Quantity`
- `Prodaja` is already treated as a finished sale, not an order lifecycle
- staff checkout currently creates `Prodaja` rows and immediately subtracts `Tepih.Quantity`

That means the safest path is:

1. Keep `Tepih` as the source of truth for products.
2. Extend `Tepih` with storefront-specific fields needed by the public shop.
3. Add new commerce tables for images, orders, status history, and stock reservations.
4. Keep writing `Prodaja` only when an order is completed/fulfilled, so existing reports continue to work.

## Exact EF Core schema

### 1. Extend `Tepih`

Add these properties to `Models/Tepih.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Inventar.Models
{
    public class Tepih
    {
        public int Id { get; set; }

        [StringLength(50)]
        public string Name { get; set; }

        [StringLength(20)]
        public string ProductNumber { get; set; }

        [StringLength(30)]
        public string Model { get; set; }

        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public string? DateTime { get; set; }

        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }

        public string? QRCodeUrl { get; set; }

        [Range(0, int.MaxValue)]
        public int? Length { get; set; }

        [Range(0, int.MaxValue)]
        public int? Width { get; set; }

        [StringLength(40)]
        public string Color { get; set; }

        [Range(0, int.MaxValue)]
        public decimal Price { get; set; }

        public bool PerM2 { get; set; }
        public string? Description { get; set; }
        public bool Disabled { get; set; }

        // Storefront fields
        public bool IsPublished { get; set; }

        [StringLength(160)]
        public string? Slug { get; set; }

        [Range(0, int.MaxValue)]
        public decimal? OnlinePrice { get; set; }

        [StringLength(240)]
        public string? ShortDescription { get; set; }

        [StringLength(160)]
        public string? SeoTitle { get; set; }

        [StringLength(320)]
        public string? SeoDescription { get; set; }

        [Range(0, int.MaxValue)]
        public int ReservedQuantity { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public virtual ICollection<Prodaja> Prodaje { get; set; } = new List<Prodaja>();
        public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();
        public virtual ICollection<WebOrderItem> WebOrderItems { get; set; } = new List<WebOrderItem>();
        public virtual ICollection<InventoryReservation> InventoryReservations { get; set; } = new List<InventoryReservation>();
    }
}
```

Notes:

- `Quantity` stays as physical on-hand stock.
- `ReservedQuantity` is stock currently held by unfinished web orders.
- storefront availability becomes `Quantity - ReservedQuantity`.
- `OnlinePrice` lets you keep a separate public price if needed. If `null`, fall back to `Price`.

### 2. Add `ProductImage`

Create `Models/ProductImage.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Inventar.Models
{
    public class ProductImage
    {
        public int Id { get; set; }

        public int TepihId { get; set; }

        [Required]
        [StringLength(200)]
        public string CloudinaryPublicId { get; set; } = null!;

        [Required]
        [StringLength(500)]
        public string Url { get; set; } = null!;

        [StringLength(500)]
        public string? ThumbnailUrl { get; set; }

        [StringLength(160)]
        public string? AltText { get; set; }

        public bool IsPrimary { get; set; }
        public int SortOrder { get; set; }
        public bool Disabled { get; set; }
        public DateTime CreatedUtc { get; set; }

        public virtual Tepih Tepih { get; set; } = null!;
    }
}
```

### 3. Add `WebOrder`

Create `Models/WebOrder.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Inventar.Models
{
    public class WebOrder
    {
        public int Id { get; set; }

        [Required]
        [StringLength(30)]
        public string OrderNumber { get; set; } = null!;

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = WebOrderStatuses.Pending;

        [Required]
        [StringLength(50)]
        public string CustomerFirstName { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string CustomerLastName { get; set; } = null!;

        [Required]
        [StringLength(254)]
        public string CustomerEmail { get; set; } = null!;

        [StringLength(30)]
        public string? CustomerPhone { get; set; }

        [StringLength(200)]
        public string ShippingAddressLine1 { get; set; } = null!;

        [StringLength(200)]
        public string? ShippingAddressLine2 { get; set; }

        [StringLength(100)]
        public string ShippingCity { get; set; } = null!;

        [StringLength(20)]
        public string? ShippingPostalCode { get; set; }

        [StringLength(100)]
        public string ShippingCountry { get; set; } = null!;

        [StringLength(200)]
        public string? BillingAddressLine1 { get; set; }

        [StringLength(200)]
        public string? BillingAddressLine2 { get; set; }

        [StringLength(100)]
        public string? BillingCity { get; set; }

        [StringLength(20)]
        public string? BillingPostalCode { get; set; }

        [StringLength(100)]
        public string? BillingCountry { get; set; }

        [StringLength(30)]
        public string Currency { get; set; } = "EUR";

        public decimal ItemsTotal { get; set; }
        public decimal ShippingTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal GrandTotal { get; set; }

        [StringLength(40)]
        public string PaymentStatus { get; set; } = WebPaymentStatuses.Pending;

        [StringLength(40)]
        public string FulfillmentStatus { get; set; } = WebFulfillmentStatuses.Unfulfilled;

        [StringLength(100)]
        public string? PaymentProvider { get; set; }

        [StringLength(200)]
        public string? PaymentReference { get; set; }

        public DateTime CreatedUtc { get; set; }
        public DateTime? PaidUtc { get; set; }
        public DateTime? CancelledUtc { get; set; }
        public DateTime? CompletedUtc { get; set; }

        public string? CustomerNote { get; set; }
        public string? InternalNote { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public virtual ICollection<WebOrderItem> Items { get; set; } = new List<WebOrderItem>();
        public virtual ICollection<WebOrderStatusHistory> StatusHistory { get; set; } = new List<WebOrderStatusHistory>();
        public virtual ICollection<InventoryReservation> Reservations { get; set; } = new List<InventoryReservation>();
    }
}
```

### 4. Add `WebOrderItem`

Create `Models/WebOrderItem.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Inventar.Models
{
    public class WebOrderItem
    {
        public int Id { get; set; }
        public int WebOrderId { get; set; }
        public int TepihId { get; set; }

        // Snapshot fields so old orders stay correct even if product data changes later.
        [Required]
        [StringLength(50)]
        public string ProductName { get; set; } = null!;

        [Required]
        [StringLength(20)]
        public string ProductNumber { get; set; } = null!;

        [StringLength(30)]
        public string? Model { get; set; }

        [StringLength(40)]
        public string? Color { get; set; }

        public int? Length { get; set; }
        public int? Width { get; set; }
        public bool PerM2 { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }

        [StringLength(500)]
        public string? PrimaryImageUrl { get; set; }

        public virtual WebOrder WebOrder { get; set; } = null!;
        public virtual Tepih Tepih { get; set; } = null!;
    }
}
```

### 5. Add `WebOrderStatusHistory`

Create `Models/WebOrderStatusHistory.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Inventar.Models
{
    public class WebOrderStatusHistory
    {
        public int Id { get; set; }
        public int WebOrderId { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = null!;

        [StringLength(50)]
        public string? ChangedBy { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        public DateTime ChangedUtc { get; set; }

        public virtual WebOrder WebOrder { get; set; } = null!;
    }
}
```

### 6. Add `InventoryReservation`

Create `Models/InventoryReservation.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Inventar.Models
{
    public class InventoryReservation
    {
        public int Id { get; set; }
        public int WebOrderId { get; set; }
        public int TepihId { get; set; }
        public int Quantity { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = InventoryReservationStatuses.Active;

        public DateTime CreatedUtc { get; set; }
        public DateTime? ExpiresUtc { get; set; }
        public DateTime? ReleasedUtc { get; set; }

        [StringLength(100)]
        public string? Reason { get; set; }

        public virtual WebOrder WebOrder { get; set; } = null!;
        public virtual Tepih Tepih { get; set; } = null!;
    }
}
```

### 7. Add status constants

Create `Models/WebOrderStatuses.cs`:

```csharp
namespace Inventar.Models
{
    public static class WebOrderStatuses
    {
        public const string Pending = "Pending";
        public const string AwaitingPayment = "AwaitingPayment";
        public const string Paid = "Paid";
        public const string Processing = "Processing";
        public const string Shipped = "Shipped";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
        public const string Refunded = "Refunded";
    }

    public static class WebPaymentStatuses
    {
        public const string Pending = "Pending";
        public const string Authorized = "Authorized";
        public const string Paid = "Paid";
        public const string Failed = "Failed";
        public const string Refunded = "Refunded";
    }

    public static class WebFulfillmentStatuses
    {
        public const string Unfulfilled = "Unfulfilled";
        public const string Processing = "Processing";
        public const string Shipped = "Shipped";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
    }

    public static class InventoryReservationStatuses
    {
        public const string Active = "Active";
        public const string Released = "Released";
        public const string Converted = "Converted";
        public const string Expired = "Expired";
    }
}
```

### 8. Update `ApplicationDbContext`

Recommended `ApplicationDbContext` shape:

```csharp
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
            });

            builder.Entity<ProductImage>(entity =>
            {
                entity.ToTable("ProductImages", "commerce");
                entity.Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
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
                entity.HasOne(x => x.Tepih)
                    .WithMany(x => x.InventoryReservations)
                    .HasForeignKey(x => x.TepihId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
```

## Order flow to preserve current reporting

Use this lifecycle:

1. Customer checks out.
2. Create `WebOrder` + `WebOrderItems`.
3. Create `InventoryReservation` rows.
4. Increase `Tepih.ReservedQuantity`.
5. Do **not** write `Prodaja` yet.
6. When payment is confirmed and the order is fulfilled:
   - reduce `Tepih.Quantity`
   - reduce `Tepih.ReservedQuantity`
   - mark reservation as `Converted`
   - write `Prodaja` rows so existing staff reports still work
7. If order is cancelled or expires:
   - reduce `Tepih.ReservedQuantity`
   - mark reservation as `Released` or `Expired`
   - do not write `Prodaja`

This keeps your old reporting logic alive while adding a real order workflow.

## Current code points that must change later

### Current product model

`Models/Tepih.cs` currently only exposes one stock number, `Quantity`. That is why the storefront needs `ReservedQuantity` and `RowVersion`.

### Current staff sale flow

In `Controllers/InventoryItemController.cs`, the `ScannedProductsToBePurchased` POST action currently:

- creates `Prodaja`
- subtracts `Tepih.Quantity` immediately

That behavior is correct for a direct in-store sale, but it is not enough for web checkout because a web order can be pending, unpaid, cancelled, or timed out.

When the storefront is introduced, this action should also validate against:

```text
available = Quantity - ReservedQuantity
```

not only against `Quantity`.

## Image strategy

Use Cloudinary for product images too, not SQL Server blob storage.

You already have Cloudinary wired into the current project, so reuse the same service idea but separate folders:

- `TepisiQRCodes` for QR codes
- `StorefrontProducts` for product images

Recommended upload rule:

- upload original image to Cloudinary
- save only metadata in `ProductImage`
- store one primary image and optional gallery images
- render thumbnails from Cloudinary transformations/CDN

Do not store raw image bytes in SQL Server.

## Exact migration order

### Migration 1: storefront-safe product columns

Add to `Tepisi`:

- `IsPublished bit not null default 0`
- `Slug nvarchar(160) null`
- `OnlinePrice decimal(18,2) null`
- `ShortDescription nvarchar(240) null`
- `SeoTitle nvarchar(160) null`
- `SeoDescription nvarchar(320) null`
- `ReservedQuantity int not null default 0`
- `RowVersion rowversion`

Why first:

- zero functional risk to the existing admin app
- no new checkout behavior yet
- lets you start preparing online catalog data early

Suggested command:

```powershell
dotnet ef migrations add AddStorefrontProductFields
```

### Migration 2: images + orders + reservations

Create:

- `commerce.ProductImages`
- `commerce.WebOrders`
- `commerce.WebOrderItems`
- `commerce.WebOrderStatusHistory`
- `commerce.InventoryReservations`

Also create the `commerce` schema if it does not exist.

Suggested command:

```powershell
dotnet ef migrations add AddCommerceTables
```

### Migration 3: backfill product slugs and online defaults

Backfill:

- `Slug` from `Name + ProductNumber + size + color`
- `OnlinePrice = Price` where you want same pricing
- `IsPublished = 0` by default until staff explicitly publishes

This migration can be raw SQL or a one-time admin tool.

Suggested command:

```powershell
dotnet ef migrations add BackfillStorefrontCatalogData
```

### Migration 4: storefront admin tooling

After schema exists, update the inventory admin UI to:

- upload product images
- mark products as published/unpublished
- edit `OnlinePrice`
- show `Available = Quantity - ReservedQuantity`
- show order list/statuses

This is an application change, not a schema-only migration.

### Migration 5: switch reporting integration on completion

When the order management flow is ready:

- create a service that converts completed `WebOrder` items into `Prodaja`
- update staff sale validation to use `Quantity - ReservedQuantity`
- add a scheduled cleanup for expired reservations

At this stage the storefront can go live.

## Storefront transaction rules

These rules are important:

### Create order

Inside one transaction:

1. Load `Tepih` rows for the basket.
2. Check `Quantity - ReservedQuantity >= requested quantity`.
3. Insert `WebOrder`.
4. Insert `WebOrderItems`.
5. Insert `InventoryReservation`.
6. Increment `ReservedQuantity`.
7. Commit.

### Cancel or expire order

Inside one transaction:

1. Load active reservations for the order.
2. Reduce `ReservedQuantity`.
3. Mark reservations as `Released` or `Expired`.
4. Update order status to `Cancelled`.
5. Commit.

### Complete order

Inside one transaction:

1. Load active reservations.
2. Reduce `Quantity`.
3. Reduce `ReservedQuantity`.
4. Mark reservations as `Converted`.
5. Create `Prodaja` rows.
6. Update order status to `Completed`.
7. Commit.

## Production rollout plan

1. Back up the SQL Server database.
2. Deploy schema-only changes first.
3. Keep all new products unpublished by default.
4. Add storefront admin controls inside the current inventory app.
5. Build the separate public web app against the same DB.
6. Launch with guest checkout first.
7. Add payment provider/webhooks after the reservation flow is stable.

## Extra recommendation before launch

Move third-party secrets out of `appsettings.json` and into environment variables, user secrets, or deployment secrets storage before the public storefront goes live.
