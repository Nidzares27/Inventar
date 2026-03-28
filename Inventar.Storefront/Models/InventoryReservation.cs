using System.ComponentModel.DataAnnotations;

namespace Inventar.Storefront.Models;

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

    public WebOrder WebOrder { get; set; } = null!;
    public StorefrontProduct Product { get; set; } = null!;
}
