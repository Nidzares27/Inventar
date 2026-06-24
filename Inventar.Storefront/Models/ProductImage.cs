using System.ComponentModel.DataAnnotations;

namespace Inventar.Storefront.Models;

public class ProductImage
{
    public int Id { get; set; }
    public int TepihId { get; set; }

    [Required]
    [StringLength(200)]
    public string CloudinaryPublicId { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Url { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ThumbnailUrl { get; set; }

    [StringLength(160)]
    public string? AltText { get; set; }

    [Required]
    [StringLength(20)]
    public string MediaType { get; set; } = "image";

    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
    public bool Disabled { get; set; }
    public DateTime CreatedUtc { get; set; }

    public StorefrontProduct Product { get; set; } = null!;
}
