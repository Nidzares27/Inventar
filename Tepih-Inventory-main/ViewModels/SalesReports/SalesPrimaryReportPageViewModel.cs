namespace Inventar.ViewModels.SalesReports
{
    public class SalesPrimaryReportPageViewModel
    {
        public List<SalesPrimaryReportGroupViewModel> Groups { get; set; } = new();
        public List<string> Options { get; set; } = new();
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
