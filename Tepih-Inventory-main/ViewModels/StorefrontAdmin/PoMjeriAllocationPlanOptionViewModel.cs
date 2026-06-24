namespace Inventar.ViewModels.StorefrontAdmin;

public class PoMjeriAllocationPlanOptionViewModel
{
    public string PlanKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int TotalLeftover { get; set; }
    public int UsedSourceCount { get; set; }
}
