using Inventar.Storefront.Models;

namespace Inventar.Storefront.Services;

public static class StorefrontStatusText
{
    public static string ToSerbianOrderStatus(string? status)
    {
        return status switch
        {
            StorefrontOrderStatuses.Pending => "Na čekanju",
            StorefrontOrderStatuses.AwaitingPayment => "Ceka uplatu",
            StorefrontOrderStatuses.Processing => "U obradi",
            StorefrontOrderStatuses.Completed => "Završena",
            StorefrontOrderStatuses.Cancelled => "Otkazana",
            _ when string.IsNullOrWhiteSpace(status) => "Nepoznato",
            _ => status
        };
    }
}
