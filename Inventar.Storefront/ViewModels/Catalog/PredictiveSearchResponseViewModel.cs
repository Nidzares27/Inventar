namespace Inventar.Storefront.ViewModels.Catalog;

public class PredictiveSearchResponseViewModel
{
    public string Query { get; set; } = string.Empty;
    public string ResultsUrl { get; set; } = "/proizvodi";
    public IReadOnlyList<PredictiveSearchSuggestionViewModel> Suggestions { get; set; } =
        Array.Empty<PredictiveSearchSuggestionViewModel>();
    public IReadOnlyList<PredictiveSearchProductViewModel> Products { get; set; } =
        Array.Empty<PredictiveSearchProductViewModel>();
}
