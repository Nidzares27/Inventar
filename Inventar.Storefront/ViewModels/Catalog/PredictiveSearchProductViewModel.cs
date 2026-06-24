namespace Inventar.Storefront.ViewModels.Catalog;

public class PredictiveSearchProductViewModel
{
    public string Url { get; set; } = "/proizvodi";
    public string? ImageUrl { get; set; }
    public string ShortDescription { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
}
