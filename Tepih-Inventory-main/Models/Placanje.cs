using System.ComponentModel.DataAnnotations;

namespace Inventar.Models
{
    public class Placanje
    {
        public int Id { get; set; }
        [StringLength(50)]
        public string CustomerName { get; set; }
        public decimal Amount { get; set; } /*double*/
        [StringLength(20)]
        public string? PaymentType { get; set; }
        public DateTime PaymentTime { get; set; }
    }
}
