using Inventar.Models;

namespace Inventar.ViewModels.StorefrontAdmin
{
    public class WebOrderAdminDetailsViewModel
    {
        public WebOrder Order { get; set; } = null!;
        public IReadOnlyList<WebOrderItem> Items { get; set; } = Array.Empty<WebOrderItem>();
        public IReadOnlyList<WebOrderStatusHistory> StatusHistory { get; set; } = Array.Empty<WebOrderStatusHistory>();
        public IReadOnlyList<InventoryReservation> Reservations { get; set; } = Array.Empty<InventoryReservation>();
        public WebOrderStatusUpdateViewModel StatusUpdate { get; set; } = new();
        public IReadOnlyList<string> AvailableOrderStatuses { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> AvailablePaymentStatuses { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> AvailableFulfillmentStatuses { get; set; } = Array.Empty<string>();
    }
}
