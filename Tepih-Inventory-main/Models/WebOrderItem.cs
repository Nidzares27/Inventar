using System.ComponentModel.DataAnnotations;

namespace Inventar.Models
{
    public class WebOrderItem
    {
        public int Id { get; set; }
        public int WebOrderId { get; set; }
        public int TepihId { get; set; }

        // Snapshot fields so old orders stay correct even if product data changes later.
        [Required]
        [StringLength(50)]
        public string ProductName { get; set; } = null!;

        [Required]
        [StringLength(20)]
        public string ProductNumber { get; set; } = null!;

        [StringLength(30)]
        public string? Model { get; set; }

        [StringLength(40)]
        public string? Color { get; set; }

        public int? Length { get; set; }
        public int? Width { get; set; }
        public bool PerM2 { get; set; }
        public bool PoMjeri { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }

        [StringLength(500)]
        public string? PrimaryImageUrl { get; set; }

        public virtual WebOrder WebOrder { get; set; } = null!;
        public virtual Tepih Tepih { get; set; } = null!;
        public virtual ICollection<InventoryReservation> Reservations { get; set; } = new List<InventoryReservation>();
    }
}
