namespace Inventar.ViewModels.Sales
{
    public class SalesReportViewModel
    {
        public int ProductId { get; set; }
        public string ProductNumber { get; set; }
        public string Name { get; set; }
        public string Model { get; set; }
        public int? Length { get; set; }
        public int? Width { get; set; }
        public string? Size { get; set; }
        public string Color { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal Price { get; set; }
        public bool PerM2 { get; set; }
    }
}