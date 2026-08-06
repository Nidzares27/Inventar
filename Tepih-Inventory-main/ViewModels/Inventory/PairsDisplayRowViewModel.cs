namespace Inventar.ViewModels.Inventory
{
    public class PairsDisplayRowViewModel
    {
        public List<PairsDisplayCellViewModel?> GroupCells { get; set; } = new();
        public string Size { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public bool IsTopLevelStart { get; set; }
        public string DetailCssClass { get; set; } = string.Empty;
        public bool IsThirdColumnGroupEnd { get; set; }
    }
}
