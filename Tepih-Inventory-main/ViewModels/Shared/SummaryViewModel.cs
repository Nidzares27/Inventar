using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels.Shared
{
    public class SummaryViewModel
    {
        public string CustomerFullName { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        [Display(Name = "Sale Time")]
        public DateTime VrijemeProdaje { get; set; }
        public string PlannedPaymentType { get; set; }
        public string Prodavac { get; set; }
        public decimal? M2Total { get; set; }
        public decimal TotalPrice { get; set; }
        public int TotalQuantity { get; set; }
        public int CustomerId { get; set; }
    }
}