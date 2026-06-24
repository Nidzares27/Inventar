namespace Inventar.ViewModels.InventoryReports
{
    public class InventoryColorReportPageViewModel
    {
        public List<InventoryColorReportGroupViewModel> Groups { get; set; } = new();
        public List<string> ColorOptions { get; set; } = new();
    }
}
