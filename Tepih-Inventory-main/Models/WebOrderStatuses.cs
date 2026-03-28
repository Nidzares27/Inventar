namespace Inventar.Models
{
    public static class WebOrderStatuses
    {
        public const string Pending = "Pending";
        public const string AwaitingPayment = "AwaitingPayment";
        public const string Paid = "Paid";
        public const string Processing = "Processing";
        public const string Shipped = "Shipped";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
        public const string Refunded = "Refunded";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Pending,
            AwaitingPayment,
            Paid,
            Processing,
            Shipped,
            Completed,
            Cancelled,
            Refunded
        };
    }

    public static class WebPaymentStatuses
    {
        public const string Pending = "Pending";
        public const string Authorized = "Authorized";
        public const string Paid = "Paid";
        public const string Failed = "Failed";
        public const string Refunded = "Refunded";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Pending,
            Authorized,
            Paid,
            Failed,
            Refunded
        };
    }

    public static class WebFulfillmentStatuses
    {
        public const string Unfulfilled = "Unfulfilled";
        public const string Processing = "Processing";
        public const string Shipped = "Shipped";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Unfulfilled,
            Processing,
            Shipped,
            Completed,
            Cancelled
        };
    }

    public static class InventoryReservationStatuses
    {
        public const string Active = "Active";
        public const string Released = "Released";
        public const string Converted = "Converted";
        public const string Expired = "Expired";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Active,
            Released,
            Converted,
            Expired
        };
    }
}
