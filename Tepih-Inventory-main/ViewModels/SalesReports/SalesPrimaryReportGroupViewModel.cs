namespace Inventar.ViewModels.SalesReports
{
    public class SalesPrimaryReportGroupViewModel
    {
        public string KeyLabel { get; set; } = string.Empty;
        public List<SalesProductSummaryViewModel> ProductRows { get; set; } = new();
        public decimal TotalM2 { get; set; }
        public int TotalQuantity { get; set; }

        public int RowSpan => Math.Max(ProductRows.Count, 1) + 1;
    }
}
