namespace Inventar.ViewModels.SalesReports
{
    public class SalesColorReportProductGroupViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public List<SalesColorReportSizeSummaryViewModel> SizeRows { get; set; } = new();
        public decimal TotalM2 { get; set; }
        public int TotalQuantity { get; set; }

        public int RowSpan => Math.Max(SizeRows.Count, 1) + 1;
    }
}
