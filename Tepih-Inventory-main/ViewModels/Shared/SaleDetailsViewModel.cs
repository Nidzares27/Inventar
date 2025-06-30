using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels.Shared
{
    public class SaleDetailsViewModel
    {
        public int Id { get; set; }
        [Display(Name = "Product ID")]
        public int TepihId { get; set; }
        public string Name { get; set; }
        public string ProductNumber { get; set; }
        public string Model { get; set; }
        [Range(1, int.MaxValue)]
        public int? Length { get; set; }
        [Range(1, int.MaxValue)]
        public int? Width { get; set; }
        public decimal? M2PerUnit { get; set; }
        public decimal? M2Total { get; set; }
        public string Color { get; set; }
        public decimal Price { get; set; }
        public bool PerM2 { get; set; }
        public decimal PriceTotal { get; set; }
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }
        public bool Disabled { get; set; }
        public string Seller { get; set; } //dodato

    }
}