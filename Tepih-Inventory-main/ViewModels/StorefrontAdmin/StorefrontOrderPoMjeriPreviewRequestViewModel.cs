using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels.StorefrontAdmin;

public class StorefrontOrderPoMjeriPreviewRequestViewModel
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int CustomWidth { get; set; }

    [Range(1, int.MaxValue)]
    public int CustomLength { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}
