using System.ComponentModel.DataAnnotations;

namespace Inventar.Models
{
    public class Kupac
    {
        public int Id { get; set; }
        [Display(Name = "Customer Full Name")]
        [StringLength(50)]
        public string CustomerFullName { get; set; }
        public decimal? LeftToPay { get; set; } /*double*/
    }
}
