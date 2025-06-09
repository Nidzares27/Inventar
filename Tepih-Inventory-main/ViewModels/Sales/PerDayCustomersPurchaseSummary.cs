namespace Inventar.ViewModels.Sales
{
    public class PerDayCustomersPurchaseSummary
    {
        public string CustomerName { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal? M2Total { get; set; }
        public int TotalQuantity { get; set; }
    }
}