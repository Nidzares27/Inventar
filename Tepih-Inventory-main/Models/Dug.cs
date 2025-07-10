using System.ComponentModel.DataAnnotations;

namespace Inventar.Models
{
    public class Dug
    {
        public int Id { get; set; }
        [Display(Name = "Customer Full Name")]
        [StringLength(50)]
        public string CustomerFullName { get; set; }
        public decimal DebtAmount { get; set; }
        public DateTime DebtTime { get; set; }

    }
}
