namespace Inventar.ViewModels.InventoryReports
{
    public class InventoryColorReportProductGroupViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public List<InventoryColorReportSizeSummaryViewModel> SizeRows { get; set; } = new();
        public decimal TotalM2 { get; set; }
        public int TotalQuantity { get; set; }

        public int RowSpan => Math.Max(SizeRows.Count, 1) + 1;
    }
}
