using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels.Buyer
{
    public class BuyerViewModel
    {
        public int Id { get; set; }
        [Display(Name = "Customer Full Name")]
        public string CustomerFullName { get; set; }
        public decimal? LeftToPay { get; set; }
        public decimal? Paid { get; set; }
    }
}
