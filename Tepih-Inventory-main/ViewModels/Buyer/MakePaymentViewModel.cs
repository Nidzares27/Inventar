namespace Inventar.ViewModels.Buyer
{
    public class MakePaymentViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal AmountPaid { get; set; }
        public string PaymentType { get; set; }//obavezno
    }
}
