using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels.StorefrontAdmin;

public class StorefrontOrderItemsUpdateViewModel
{
    [Range(1, int.MaxValue)]
    public int WebOrderId { get; set; }

    public List<StorefrontOrderItemEditInputViewModel> Items { get; set; } = new();
}
