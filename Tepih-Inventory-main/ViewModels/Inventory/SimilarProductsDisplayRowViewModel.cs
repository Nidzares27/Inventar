namespace Inventar.ViewModels.Inventory
{
    public class SimilarProductsDisplayRowViewModel
    {
        public string ProductNumber { get; set; } = "-";
        public decimal Price { get; set; }
        public string Color { get; set; } = "-";
        public bool ShowColor { get; set; }
        public int ColorRowSpan { get; set; }
        public string Size { get; set; } = "-";
        public decimal? M2 { get; set; }
        public int Quantity { get; set; }
        public decimal? M2Total { get; set; }
        public bool IsGroupStart { get; set; }
    }
}
