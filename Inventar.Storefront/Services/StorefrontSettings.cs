namespace Inventar.Storefront.Services;

public class StorefrontSettings
{
    public const string SectionName = "Storefront";

    public string BrandName { get; set; } = "Kašmir Home";
    public int ReservationHours { get; set; } = 240;
    public decimal FlatShippingCost { get; set; } = 3.50m;
    public int RememberCustomerForDays { get; set; } = 30;
    public int MaxLoginCodeAttempts { get; set; } = 5;
}
