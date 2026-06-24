namespace Inventar.ViewModels.InventoryReports
{
    public class InventoryErrorReportGroupViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public List<InventoryErrorReportItemViewModel> Items { get; set; } = new();
        public decimal TotalM2 { get; set; }
        public int TotalQuantity { get; set; }
    }
}
