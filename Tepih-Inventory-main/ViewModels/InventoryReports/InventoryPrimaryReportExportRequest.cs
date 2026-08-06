namespace Inventar.ViewModels.InventoryReports
{
    public class InventoryPrimaryReportExportRequest
    {
        public string Heading { get; set; } = string.Empty;
        public List<string> Keys { get; set; } = new();
        public bool UseCustomTable { get; set; }
    }
}
