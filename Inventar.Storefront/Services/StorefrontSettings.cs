namespace Inventar.Storefront.Services;

public class StorefrontSettings
{
    public const string SectionName = "Storefront";

    public string BrandName { get; set; } = "Tepih Studio";
    public int ReservationHours { get; set; } = 48;
    public decimal FlatShippingCost { get; set; } = 3.50m;
}
