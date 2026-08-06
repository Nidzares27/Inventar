namespace Inventar.ViewModels.Inventory
{
    public class SimilarProductsLookupResultViewModel
    {
        public string? ErrorMessage { get; set; }
        public SimilarProductSummaryViewModel? Summary { get; set; }
        public List<SimilarProductsDisplayRowViewModel> Rows { get; set; } = new();
    }
}
