namespace Inventar.ViewModels.InventoryReports
{
    public class InventoryErrorReportItemViewModel
    {
        public string ProductNumber { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string SizeLabel { get; set; } = string.Empty;
        public decimal M2 { get; set; }
        public int Quantity { get; set; }
        public decimal M2Total { get; set; }
    }
}
