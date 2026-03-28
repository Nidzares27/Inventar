using System.ComponentModel.DataAnnotations;

namespace Inventar.Models
{
    public class WebOrder
    {
        public int Id { get; set; }

        [Required]
        [StringLength(30)]
        public string OrderNumber { get; set; } = null!;

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = WebOrderStatuses.Pending;

        [Required]
        [StringLength(50)]
        public string CustomerFirstName { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string CustomerLastName { get; set; } = null!;

        [Required]
        [StringLength(254)]
        public string CustomerEmail { get; set; } = null!;

        [StringLength(30)]
        public string? CustomerPhone { get; set; }

        [StringLength(200)]
        public string ShippingAddressLine1 { get; set; } = null!;

        [StringLength(200)]
        public string? ShippingAddressLine2 { get; set; }

        [StringLength(100)]
        public string ShippingCity { get; set; } = null!;

        [StringLength(20)]
        public string? ShippingPostalCode { get; set; }

        [StringLength(100)]
        public string ShippingCountry { get; set; } = null!;

        [StringLength(200)]
        public string? BillingAddressLine1 { get; set; }

        [StringLength(200)]
        public string? BillingAddressLine2 { get; set; }

        [StringLength(100)]
        public string? BillingCity { get; set; }

        [StringLength(20)]
        public string? BillingPostalCode { get; set; }

        [StringLength(100)]
        public string? BillingCountry { get; set; }

        [StringLength(30)]
        public string Currency { get; set; } = "EUR";

        public decimal ItemsTotal { get; set; }
        public decimal ShippingTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal GrandTotal { get; set; }

        [StringLength(40)]
        public string PaymentStatus { get; set; } = WebPaymentStatuses.Pending;

        [StringLength(40)]
        public string FulfillmentStatus { get; set; } = WebFulfillmentStatuses.Unfulfilled;

        [StringLength(100)]
        public string? PaymentProvider { get; set; }

        [StringLength(200)]
        public string? PaymentReference { get; set; }

        public DateTime CreatedUtc { get; set; }
        public DateTime? PaidUtc { get; set; }
        public DateTime? CancelledUtc { get; set; }
        public DateTime? CompletedUtc { get; set; }

        public string? CustomerNote { get; set; }
        public string? InternalNote { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public virtual ICollection<WebOrderItem> Items { get; set; } = new List<WebOrderItem>();
        public virtual ICollection<WebOrderStatusHistory> StatusHistory { get; set; } = new List<WebOrderStatusHistory>();
        public virtual ICollection<InventoryReservation> Reservations { get; set; } = new List<InventoryReservation>();
    }
}
