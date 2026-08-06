namespace Inventar.ViewModels.Inventory
{
    public class PairsNameGroupViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public int RowSpan { get; set; }
        public List<PairsModelGroupViewModel> ModelGroups { get; set; } = new();
    }
}
