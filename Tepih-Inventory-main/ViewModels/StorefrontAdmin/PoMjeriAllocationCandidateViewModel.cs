namespace Inventar.ViewModels.StorefrontAdmin;

public class PoMjeriAllocationCandidateViewModel
{
    public int ProductId { get; set; }
    public string UnID { get; set; } = string.Empty;
    public int OriginalWidth { get; set; }
    public int OriginalLength { get; set; }
    public int RemainingWidth { get; set; }
    public int RemainingLength { get; set; }
    public int ConsumedLengthPerUnit { get; set; }
    public int MaxAvailableQuantity { get; set; }
    public int CurrentAllocatedQuantity { get; set; }
}
