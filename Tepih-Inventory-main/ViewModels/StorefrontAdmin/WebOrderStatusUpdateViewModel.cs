namespace Inventar.ViewModels.StorefrontAdmin
{
    public class WebOrderStatusUpdateViewModel
    {
        public int WebOrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string FulfillmentStatus { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string? InternalNote { get; set; }
    }
}
