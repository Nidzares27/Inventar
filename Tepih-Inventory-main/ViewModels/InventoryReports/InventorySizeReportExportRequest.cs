namespace Inventar.ViewModels.InventoryReports
{
    public class InventorySizeReportExportRequest
    {
        public string Heading { get; set; } = string.Empty;
        public List<string> Sizes { get; set; } = new();
        public bool UseCustomTable { get; set; }
    }
}
