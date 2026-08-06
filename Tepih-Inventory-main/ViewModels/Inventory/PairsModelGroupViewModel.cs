namespace Inventar.ViewModels.Inventory
{
    public class PairsModelGroupViewModel
    {
        public string Model { get; set; } = string.Empty;
        public int RowSpan { get; set; }
        public List<PairsColorGroupViewModel> ColorGroups { get; set; } = new();
    }
}
