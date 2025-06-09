namespace Inventar.ViewModels.Buyer.DTO
{
    public class PaymentPdfRequest
    {
        public string Heading { get; set; }
        public List<string[]> Data { get; set; }
        public Dictionary<int, string> Filters { get; set; }
        public string MinDate { get; set; }
        public string MaxDate { get; set; }
        public List<string> ColumnHeaders { get; set; }
        public decimal TotalPrice { get; set; }
    }
}
