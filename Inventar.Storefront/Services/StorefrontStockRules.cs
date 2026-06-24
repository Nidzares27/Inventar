namespace Inventar.Storefront.Services;

public static class StorefrontStockRules
{
    public const string SoldOutStatusText = "Rasprodato";
    public const string SoldOutOverlayText = "Rasprodato";

    public static int GetMaxOrderQuantity(int availableQuantity)
    {
        return Math.Max(availableQuantity, 0);
    }

    public static string BuildQuantityLimitMessage(int requestedQuantity, int availableQuantity)
    {
        var allowedQuantity = GetMaxOrderQuantity(availableQuantity);
        if (allowedQuantity <= 0)
        {
            return "Odabrani proizvod trenutno nije dostupan.";
        }

        if (requestedQuantity > allowedQuantity)
        {
            return $"Mogu\u0107e je naru\u010Diti najvi\u0161e {allowedQuantity} komada za dati proizvod!";
        }

        return string.Empty;
    }

    public static string BuildAvailabilityStatusMessage(int availableQuantity)
    {
        return availableQuantity switch
        {
            <= 0 => SoldOutStatusText,
            <= 3 => $"Jo\u0161 samo {availableQuantity} komada u zalihama!",
            _ => string.Empty
        };
    }
}
