using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels.StorefrontAdmin;

public class StorefrontOrderItemEditInputViewModel
{
    public int? ExistingItemId { get; set; }

    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    public bool PoMjeri { get; set; }
    public bool PerM2 { get; set; }
    public string? Color { get; set; }

    [Range(1, int.MaxValue)]
    public int? Width { get; set; }

    [Range(1, int.MaxValue)]
    public int? Length { get; set; }
}
