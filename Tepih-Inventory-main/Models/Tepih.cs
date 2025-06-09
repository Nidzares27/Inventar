using System.ComponentModel.DataAnnotations;

namespace Inventar.Models
{
    public class Tepih
    {
        public int Id { get; set; }
        [StringLength(50)]
        public string Name { get; set; }
        [StringLength(20)]
        public string ProductNumber { get; set; }
        [StringLength(30)]
        public string Model { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public string? DateTime { get; set; }
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; } /*short*/
        public string? QRCodeUrl { get; set; }
        [Range(0, int.MaxValue)]
        public int? Length { get; set; } /*ushort*/
        [Range(0, int.MaxValue)]
        public int? Width { get; set; } /*ushort*/
        [StringLength(40)]
        public string Color { get; set; }
        [Range(0, int.MaxValue)]
        public decimal Price { get; set; } /*double*/
        public bool PerM2 { get; set; }
        public string? Description { get; set; }
        public virtual ICollection<Prodaja> Prodaje { get; set; }
    }
}
