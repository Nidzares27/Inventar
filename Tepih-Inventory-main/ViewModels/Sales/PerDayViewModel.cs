namespace Inventar.ViewModels.Sales
{
    public class PerDayViewModel
    {
        public DateOnly? Date { get; set; }
        public decimal? TotalSpentSum { get; set; }
        public decimal? TotalM2 { get; set; }
        public int? TotalQuantity { get; set; }
        public List<PerDayCustomersPurchaseSummary> SalesReport { get; set; }
    }
}