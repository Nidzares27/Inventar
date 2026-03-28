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
        public bool Disabled { get; set; }
        // Storefront fields
        public bool IsPublished { get; set; }

        [StringLength(160)]
        public string? Slug { get; set; }

        [Range(0, int.MaxValue)]
        public decimal? OnlinePrice { get; set; }

        [StringLength(240)]
        public string? ShortDescription { get; set; }

        [StringLength(160)]
        public string? SeoTitle { get; set; }

        [StringLength(320)]
        public string? SeoDescription { get; set; }

        [Range(0, int.MaxValue)]
        public int ReservedQuantity { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
        public virtual ICollection<Prodaja> Prodaje { get; set; }
        public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();
        public virtual ICollection<WebOrderItem> WebOrderItems { get; set; } = new List<WebOrderItem>();
        public virtual ICollection<InventoryReservation> InventoryReservations { get; set; } = new List<InventoryReservation>();
    }
}
