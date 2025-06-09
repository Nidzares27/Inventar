namespace Inventar.ViewModels.Sales
{
    public class SalesEntryViewModel
    {
        public DateTime VrijemeProdaje { get; set; }
        public string CustomerFullName { get; set; }
        public int ProductId { get; set; }
        public string ProductNumber { get; set; }
        public string Name { get; set; }
        public string Model { get; set; }
        public string Color { get; set; }
        public int? Length { get; set; }
        public int? Width { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public bool PerM2 { get; set; }
        public decimal? M2Total => PerM2 ? Math.Round((decimal)Length * (decimal)Width / 10000m * Quantity, 2) : null;
        public decimal TotalPrice { get; set; }
    }
}