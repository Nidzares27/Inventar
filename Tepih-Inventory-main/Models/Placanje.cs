namespace Inventar.Models
{
    public class Placanje
    {
        public int Id { get; set; }
        public string CustomerName { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentTime { get; set; }

    }
}
