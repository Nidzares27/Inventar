namespace Inventar.ViewModels.Inventory
{
    public class PairsViewModel
    {
        public PairsFilterViewModel Filter { get; set; } = new();
        public List<PairsGroupingColumnViewModel> GroupColumns { get; set; } = new();
        public List<PairsDisplayRowViewModel> Rows { get; set; } = new();
        public List<string> NameOptions { get; set; } = new();
        public List<string> ModelOptions { get; set; } = new();
        public List<string> ColorOptions { get; set; } = new();
        public bool Submitted { get; set; }
        public string? ValidationMessage { get; set; }
    }
}
