using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels.Inventory
{
    public class ScannedProductViewModel
    {
        public string LineId { get; set; } = Guid.NewGuid().ToString("N");
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
        public int? Length { get; set; }
        [Range(1, int.MaxValue)]
        public int? Width { get; set; }
        [Range(1, int.MaxValue)]
        public int? OriginalLength { get; set; }
        [Range(1, int.MaxValue)]
        public int? OriginalWidth { get; set; }
        [Range(0, int.MaxValue)]
        public int? RemainingLength { get; set; }
        [Range(0, int.MaxValue)]
        public int? RemainingWidth { get; set; }
        [Range(0, int.MaxValue)]
        public int? ConsumedLengthPerUnit { get; set; }
        [Range(0, int.MaxValue)]
        public int MaxAvailableQuantity { get; set; }
        public int? Rabat { get; set; }
        public decimal? M2PerUnit { get; set; }
        public decimal? M2Total { get; set; }
        public string Color { get; set; }
        [Range(0, int.MaxValue)]
        public decimal Price { get; set; }
        public bool PerM2 { get; set; }
        public bool PoMjeri { get; set; }
        public bool IsDirectSaleProduct { get; set; }
        public decimal? DirectSaleOriginalTotal { get; set; }
        public string? UnID { get; set; }
        public decimal PriceTotal { get; set; }
    }
}
