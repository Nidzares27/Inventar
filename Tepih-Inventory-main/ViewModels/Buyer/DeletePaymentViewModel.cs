namespace Inventar.ViewModels.Buyer
{
    public class DeleteEditPaymentViewModel
    {
        public int Id { get; set; }
        public string CustomerName { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentTime { get; set; }
        public int BuyerId { get; set; }
        public string? PaymentType { get; set; }
    }
}
