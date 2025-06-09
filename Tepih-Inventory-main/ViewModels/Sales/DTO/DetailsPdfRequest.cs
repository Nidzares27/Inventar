namespace Inventar.ViewModels.Sales.DTO
{
    public class DetailsPdfRequest
    {
        public string HeadingLeft { get; set; }    // h2 text
        public string HeadingRight { get; set; }   // h4 text
        public List<string[]> Data { get; set; }
        public List<string> Filters { get; set; }
        public List<string> ColumnHeaders { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalM2 { get; set; }
        public decimal TotalPrice { get; set; }
    }
}
