namespace Inventar.ViewModels
{
    public class SalesReportViewModel
    {
        public int ProductId { get; set; }
        public string ProductNumber { get; set; }
        public string Name { get; set; }
        public string Model { get; set; }
        public int Length { get; set; }
        public int Width { get; set; }
        public string Size { get; set; } // "Length X Width"
        public string Color { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal Price { get; set; }
        public bool PerM2 { get; set; }
        //public decimal? M2PerUnit { get; set; }
        //public decimal? M2Total { get; set; }
    }
}
