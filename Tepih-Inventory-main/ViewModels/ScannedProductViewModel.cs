using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels
{
    public class ScannedProductViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ProductNumber { get; set; }
        public string Model { get; set; }

        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public string? DateTime { get; set; }
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }
        public string? QRCodeUrl { get; set; }
        [Range(1, int.MaxValue)]
        public int Length { get; set; }
        [Range(1, int.MaxValue)]
        public int Width { get; set; }
        public int? Rabat { get; set; }
        public decimal M2PerUnit { get; set; }/* => (decimal)(Length * Width) / 100;*/
        public decimal M2Total { get; set; } /*=> M2PerUnit * Quantity;*/
        public string Color { get; set; }
        [Range(0, int.MaxValue)]
        public decimal Price { get; set; }
        public bool PerM2 { get; set; }
        public decimal PriceTotal { get; set; }
    }
}
