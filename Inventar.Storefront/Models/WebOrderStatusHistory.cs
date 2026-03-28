using System.ComponentModel.DataAnnotations;

namespace Inventar.Storefront.Models;

public class WebOrderStatusHistory
{
    public int Id { get; set; }
    public int WebOrderId { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = string.Empty;

    [StringLength(50)]
    public string? ChangedBy { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    public DateTime ChangedUtc { get; set; }

    public WebOrder WebOrder { get; set; } = null!;
}
