using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Inventar.Models
{
    public class Prodaja
    {
        public int Id { get; set; }
        [Display(Name = "Product ID")]
        public int TepihId { get; set; }
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; } /*ushort*/
        [Display(Name = "Customer Full Name")]
        [StringLength(50)]
        public string CustomerFullName { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        [Display(Name = "Sale Time")]
        public DateTime VrijemeProdaje { get; set; }
        public decimal Price { get; set; } /*double*/
        [StringLength(20)]
        public string PlannedPaymentType { get; set; } 
        [StringLength(50)]
        public string Prodavac {  get; set; }
        public bool Disabled { get; set; }

        [ForeignKey("TepihId")]
        [InverseProperty("Prodaje")]
        public virtual Tepih Tepih { get; set; } = null!;
    }
}
