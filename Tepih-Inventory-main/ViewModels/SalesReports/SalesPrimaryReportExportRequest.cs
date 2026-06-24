namespace Inventar.ViewModels.SalesReports
{
    public class SalesPrimaryReportExportRequest
    {
        public string Heading { get; set; } = string.Empty;
        public List<string> Keys { get; set; } = new();
        public bool UseCustomTable { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
