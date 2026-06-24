namespace Inventar.ViewModels.InventoryReports
{
    public class InventorySizeReportGroupViewModel
    {
        public string SizeLabel { get; set; } = string.Empty;
        public List<InventorySizeReportProductSummaryViewModel> ProductRows { get; set; } = new();
        public decimal TotalM2 { get; set; }
        public int TotalQuantity { get; set; }

        public int RowSpan => Math.Max(ProductRows.Count, 1) + 1;
    }
}
