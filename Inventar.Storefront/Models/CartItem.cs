namespace Inventar.Storefront.Models;

public class CartItem
{
    public string LineId { get; set; } = Guid.NewGuid().ToString("N");
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public bool PoMjeri { get; set; }
    public int? CustomWidth { get; set; }
    public int? CustomLength { get; set; }
    public string? SelectedColor { get; set; }
    public List<CartItemAllocation> Allocations { get; set; } = new();
}

public class CartItemAllocation
{
    public int SourceProductId { get; set; }
    public int Quantity { get; set; }
    public int ConsumedLengthPerUnit { get; set; }
}
