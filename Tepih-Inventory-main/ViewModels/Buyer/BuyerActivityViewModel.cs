using Inventar.ViewModels.Shared;

namespace Inventar.ViewModels.Buyer
{
    public class BuyerActivityViewModel
    {
        public int BuyerId { get; set; }
        public string BuyerName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<BuyerActivityItem> Activities { get; set; }
        public decimal TotalDebt { get; set; }
    }
}