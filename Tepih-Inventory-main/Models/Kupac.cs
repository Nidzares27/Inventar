using System.ComponentModel.DataAnnotations;

namespace Inventar.Models
{
    public class Kupac
    {
        public int Id { get; set; }
        [Display(Name = "Customer Full Name")]
        public string CustomerFullName { get; set; }
        public decimal? LeftToPay { get; set; }
    }
}
