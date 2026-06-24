using System.ComponentModel.DataAnnotations;

namespace Inventar.Storefront.Models;

public class StorefrontCustomer
{
    public int Id { get; set; }

    [Required]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(254)]
    public string NormalizedEmail { get; set; } = string.Empty;

    [StringLength(50)]
    public string? FirstName { get; set; }

    [StringLength(50)]
    public string? LastName { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(200)]
    public string? AddressLine1 { get; set; }

    [StringLength(200)]
    public string? AddressLine2 { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    public string Country { get; set; } = "Crna Gora";

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public DateTime? EmailVerifiedUtc { get; set; }
    public bool Disabled { get; set; }

    public ICollection<WebOrder> Orders { get; set; } = new List<WebOrder>();

    public string DisplayName =>
        string.Join(" ", new[] { FirstName, LastName }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
}
