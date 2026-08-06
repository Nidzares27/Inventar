namespace Inventar.ViewModels.InventoryReports
{
    public class InventoryPrimaryReportGroupViewModel
    {
        public string KeyLabel { get; set; } = string.Empty;
        public List<InventoryProductSummaryViewModel> ProductRows { get; set; } = new();
        public decimal TotalM2 { get; set; }
        public int TotalQuantity { get; set; }

        public int RowSpan => Math.Max(ProductRows.Count, 1) + 1;
    }
}
