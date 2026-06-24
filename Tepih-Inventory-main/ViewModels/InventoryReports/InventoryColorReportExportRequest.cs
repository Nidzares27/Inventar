namespace Inventar.ViewModels.InventoryReports
{
    public class InventoryColorReportExportRequest
    {
        public string Heading { get; set; } = string.Empty;
        public List<string> Colors { get; set; } = new();
        public bool UseCustomTable { get; set; }
    }
}
