namespace Inventar.ViewModels.InventoryReports
{
    public class InventoryNameReportSizeSummaryViewModel
    {
        public string SizeLabel { get; set; } = string.Empty;
        public decimal TotalM2 { get; set; }
        public int TotalQuantity { get; set; }
    }
}
