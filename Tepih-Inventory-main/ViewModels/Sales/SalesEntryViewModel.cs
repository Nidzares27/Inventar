using System.ComponentModel.DataAnnotations;

using Inventar.Utils;

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
        public bool PoMjeri { get; set; }
        public int? Length { get; set; }
        public int? Width { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public bool PerM2 { get; set; }
        public decimal? M2Total => PoMjeriHelper.CalculateM2Total(PerM2, Width, Length, Quantity);
        public decimal TotalPrice { get; set; }
        [Range(0, 100)]
        public int? Rabat { get; set; }
    }
}
