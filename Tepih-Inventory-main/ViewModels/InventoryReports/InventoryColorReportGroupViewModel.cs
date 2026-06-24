namespace Inventar.ViewModels.InventoryReports
{
    public class InventoryColorReportGroupViewModel
    {
        public string Color { get; set; } = string.Empty;
        public List<InventoryColorReportProductGroupViewModel> ProductGroups { get; set; } = new();
        public decimal TotalM2 { get; set; }
        public int TotalQuantity { get; set; }

        public int RowSpan => ProductGroups.Sum(productGroup => productGroup.RowSpan) + 1;
    }
}
