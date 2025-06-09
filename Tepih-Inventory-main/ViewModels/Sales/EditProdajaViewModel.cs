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
        public decimal Price { get; set; }
        public bool PerM2 { get; set; }
        public decimal? M2Total { get; set; }
        [Range(1, int.MaxValue)]
        public int? Length { get; set; }
        [Range(1, int.MaxValue)]
        public int? Width { get; set; }
        public string? Prodavac { get; set; }
    }
}