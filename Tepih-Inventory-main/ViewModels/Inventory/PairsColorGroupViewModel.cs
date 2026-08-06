namespace Inventar.ViewModels.Inventory
{
    public class PairsColorGroupViewModel
    {
        public string Color { get; set; } = string.Empty;
        public int RowSpan { get; set; }
        public List<PairsRowViewModel> SizeRows { get; set; } = new();
    }
}
