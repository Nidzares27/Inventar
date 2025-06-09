namespace Inventar.ViewModels.Sales.DTO
{
    public class DetailsUngroupedPdfRequest
    {
        public string Heading1 { get; set; }
        public string Heading2 { get; set; }
        public string Heading3 { get; set; }
        public string CustomerName { get; set; }
        public List<string[]> Data { get; set; }
        public Dictionary<int, string> Filters { get; set; }
        public string MinDate { get; set; }
        public List<string> ColumnHeaders { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalM2 { get; set; }
        public decimal TotalPrice { get; set; }

        public string ProductId { get; set; }
        public string ProductNumber { get; set; }
        public string Name { get; set; }
        public string Model { get; set; }
        public string Color { get; set; }
        public string Size { get; set; }
        public string M2PerProduct { get; set; }
    }
}
