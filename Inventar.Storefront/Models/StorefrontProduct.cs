using System.ComponentModel.DataAnnotations;

namespace Inventar.Storefront.Models;

public class StorefrontProduct
{
    public int Id { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string ProductNumber { get; set; } = string.Empty;

    [StringLength(30)]
    public string Model { get; set; } = string.Empty;

    [StringLength(40)]
    public string Color { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int? Length { get; set; }
    public int? Width { get; set; }
    public decimal Price { get; set; }
    public decimal? OnlinePrice { get; set; }
    public bool PerM2 { get; set; }
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? Slug { get; set; }
    public bool IsPublished { get; set; }
    public bool Disabled { get; set; }
    public byte[]? RowVersion { get; set; }

    public ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public int AvailableQuantity => Math.Max(Quantity - ReservedQuantity, 0);
    public decimal EffectivePrice => OnlinePrice ?? Price;
}
