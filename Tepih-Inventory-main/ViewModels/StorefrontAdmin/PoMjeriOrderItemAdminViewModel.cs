using Inventar.Models;

namespace Inventar.ViewModels.StorefrontAdmin;

public class PoMjeriOrderItemAdminViewModel
{
    public WebOrderItem Item { get; set; } = null!;
    public IReadOnlyList<InventoryReservation> ActiveReservations { get; set; } = Array.Empty<InventoryReservation>();
    public IReadOnlyList<PoMjeriAllocationCandidateViewModel> Candidates { get; set; } = Array.Empty<PoMjeriAllocationCandidateViewModel>();
}
