namespace Inventar.ViewModels.Inventory
{
    public class SimilarProductSummaryViewModel
    {
        public string ProductNumber { get; set; } = "-";
        public string ProductName { get; set; } = "-";
        public decimal Price { get; set; }
        public string Model { get; set; } = "-";
        public string Color { get; set; } = "-";
        public string Size { get; set; } = "-";
        public decimal? M2 { get; set; }
        public int Quantity { get; set; }
        public decimal? M2Total { get; set; }
    }
}
