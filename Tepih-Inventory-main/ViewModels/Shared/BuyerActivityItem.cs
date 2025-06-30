namespace Inventar.ViewModels.Shared
{
    public class BuyerActivityItem
    {
        public DateTime ActivityTime { get; set; } // For sorting
        public string Type { get; set; } // "Payment" or "Sale"
        public decimal Amount { get; set; } // Amount paid OR sum of sale total prices
        public string Info { get; set; } // PaymentType OR Prodavac
        public bool Disabled { get; set; }
    }
}