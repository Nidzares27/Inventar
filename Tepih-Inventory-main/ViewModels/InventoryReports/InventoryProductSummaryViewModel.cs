namespace Inventar.ViewModels.InventoryReports
{
    public class InventoryProductSummaryViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal TotalM2 { get; set; }
        public int TotalQuantity { get; set; }
    }
}
