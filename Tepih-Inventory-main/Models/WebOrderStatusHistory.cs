using System.ComponentModel.DataAnnotations;

namespace Inventar.Models
{
    public class WebOrderStatusHistory
    {
        public int Id { get; set; }
        public int WebOrderId { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = null!;

        [StringLength(50)]
        public string? ChangedBy { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        public DateTime ChangedUtc { get; set; }

        public virtual WebOrder WebOrder { get; set; } = null!;
    }
}
