namespace Inventar.ViewModels.InventoryReports
{
    public class InventoryPrimaryReportPageViewModel
    {
        public List<InventoryPrimaryReportGroupViewModel> Groups { get; set; } = new();
        public List<string> Options { get; set; } = new();
    }
}
