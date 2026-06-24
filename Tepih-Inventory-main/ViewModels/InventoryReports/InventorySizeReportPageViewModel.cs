namespace Inventar.ViewModels.InventoryReports
{
    public class InventorySizeReportPageViewModel
    {
        public List<InventorySizeReportGroupViewModel> Groups { get; set; } = new();
        public List<string> SizeOptions { get; set; } = new();
    }
}
