using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels
{
    public class ProdajaViewModel
    {
        public int Id { get; set; }
        [Display(Name = "Product ID")]
        public int TepihId { get; set; }
        public string Name { get; set; }
        public string ProductNumber { get; set; }
        public string Model { get; set; }
        [Range(1, int.MaxValue)]
        public int Length { get; set; }
        [Range(1, int.MaxValue)]
        public int Width { get; set; }
        public decimal M2PerUnit { get; set; }
        public decimal M2Total { get; set; } 
        public string Color { get; set; }
        public decimal Price { get; set; }
        public bool PerM2 { get; set; }
        public decimal PriceTotal { get; set; }
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }
        [Display(Name = "Customer Full Name")]
        public string CustomerFullName { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        [Display(Name = "Sale Time")]
        public DateTime VrijemeProdaje { get; set; }

    }
}
