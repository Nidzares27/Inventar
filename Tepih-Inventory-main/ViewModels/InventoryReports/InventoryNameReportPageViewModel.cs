namespace Inventar.ViewModels.InventoryReports
{
    public class InventoryNameReportPageViewModel
    {
        public List<InventoryNameReportGroupViewModel> Groups { get; set; } = new();
        public List<string> ProductNameOptions { get; set; } = new();
    }
}
