namespace Inventar.ViewModels.InventoryReports
{
    public class InventoryErrorReportExportRequest
    {
        public string Heading { get; set; } = string.Empty;
        public List<string> ProductNames { get; set; } = new();
    }
}
