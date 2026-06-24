namespace Inventar.ViewModels.InventoryReports
{
    public class InventoryPieceReportPageViewModel
    {
        public List<InventoryPieceReportGroupViewModel> Groups { get; set; } = new();
        public List<string> ProductNameOptions { get; set; } = new();
    }
}
