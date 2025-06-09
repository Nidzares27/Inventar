namespace Inventar.ViewModels.Sales
{
    public class LabelsViewModel
    {
        public int? ProductId { get; set; }
        public string ProductNumber { get; set; }
        public string Name { get; set; }
        public string? Model { get; set; }
        public string? Color { get; set; }
        public string? Size { get; set; }
        public decimal? M2PerProduct { get; set; }
        public string? CustName { get; set; }
    }
}