namespace Inventar.ViewModels.SalesReports
{
    public class SalesProductSummaryViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal TotalM2 { get; set; }
        public int TotalQuantity { get; set; }
    }
}
