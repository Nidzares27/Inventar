namespace Inventar.Storefront.Models;

public class StorefrontSale
{
    public int Id { get; set; }
    public int TepihId { get; set; }
    public int Quantity { get; set; }
    public int? CustomWidth { get; set; }
    public int? CustomLength { get; set; }
    public int? ConsumedLength { get; set; }
    public bool Disabled { get; set; }
}
