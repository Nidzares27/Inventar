namespace Inventar.ViewModels.SalesReports
{
    public class SalesColorReportExportRequest
    {
        public string Heading { get; set; } = string.Empty;
        public List<string> Colors { get; set; } = new();
        public bool UseCustomTable { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
