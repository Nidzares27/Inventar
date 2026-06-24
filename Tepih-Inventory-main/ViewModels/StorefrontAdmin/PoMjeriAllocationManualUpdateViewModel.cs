namespace Inventar.ViewModels.StorefrontAdmin;

public class PoMjeriAllocationManualUpdateViewModel
{
    public int WebOrderItemId { get; set; }
    public List<PoMjeriAllocationEntryInputViewModel> Entries { get; set; } = new();
}
