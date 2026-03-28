namespace Inventar.Storefront.Models;

public static class StorefrontOrderStatuses
{
    public const string Pending = "Pending";
    public const string AwaitingPayment = "AwaitingPayment";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public static class StorefrontPaymentStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Refunded = "Refunded";
}

public static class StorefrontFulfillmentStatuses
{
    public const string Unfulfilled = "Unfulfilled";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public static class InventoryReservationStatuses
{
    public const string Active = "Active";
    public const string Released = "Released";
    public const string Converted = "Converted";
    public const string Expired = "Expired";
}

public static class StorefrontPaymentProviders
{
    public const string CashOnDelivery = "CashOnDelivery";
}
