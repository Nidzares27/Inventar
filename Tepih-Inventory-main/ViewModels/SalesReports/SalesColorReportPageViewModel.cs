namespace Inventar.ViewModels.SalesReports
{
    public class SalesColorReportPageViewModel
    {
        public List<SalesColorReportGroupViewModel> Groups { get; set; } = new();
        public List<string> ColorOptions { get; set; } = new();
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
