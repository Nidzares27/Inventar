namespace Inventar.ViewModels
{
    public class PerDayViewModel
    {
        public DateOnly? Date { get; set; }
        public decimal? TotalSpentSum { get; set; }
        public decimal? TotalM2 { get; set; }
        public List<PerDayCustomersPurchaseSummary> SalesReport { get; set; }

    }
}
