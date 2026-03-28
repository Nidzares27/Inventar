namespace Inventar.ViewModels.StorefrontAdmin
{
    public class WebOrderAdminListItemViewModel
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string FulfillmentStatus { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public decimal GrandTotal { get; set; }
    }
}
