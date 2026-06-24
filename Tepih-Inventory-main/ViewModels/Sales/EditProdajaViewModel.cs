using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels.Sales
{
    public class EditProdajaViewModel
    {
        public int Id { get; set; }
        [Display(Name = "Product ID")]
        public int TepihId { get; set; }
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }
        [Display(Name = "Customer Full Name")]
        public string CustomerFullName { get; set; }
        [Display(Name = "Sale Time")]
        public DateTime VrijemeProdaje { get; set; }
        [Range(0, 100)]
        public int? Rabat { get; set; }
        public decimal Price { get; set; }
        public decimal? DirectSaleOriginalTotal { get; set; }
        public decimal TotalPrice { get; set; }
        public bool PerM2 { get; set; }
        public bool PoMjeri { get; set; }
        public bool IsDirectSaleProduct { get; set; }
        public decimal? M2Total { get; set; }
        [Range(1, int.MaxValue)]
        public int? Length { get; set; }
        [Range(1, int.MaxValue)]
        public int? Width { get; set; }
        [Range(1, int.MaxValue)]
        public int? OriginalLength { get; set; }
        [Range(1, int.MaxValue)]
        public int? OriginalWidth { get; set; }
        [Range(0, int.MaxValue)]
        public int? ConsumedLength { get; set; }
        public string? Prodavac { get; set; }
        public string PlannedPaymentType { get; set; }
        public string? ProductName { get; set; }
        public string? ProductModel { get; set; }
        public string? ProductColor { get; set; }
    }
}
