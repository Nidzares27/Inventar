namespace Inventar.ViewModels.InventoryReports
{
    public class InventoryNameReportExportRequest
    {
        public string Heading { get; set; } = string.Empty;
        public List<string> ProductNames { get; set; } = new();
        public bool UseCustomTable { get; set; }
    }
}
