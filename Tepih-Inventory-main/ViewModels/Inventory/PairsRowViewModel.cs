namespace Inventar.ViewModels.Inventory
{
    public class PairsRowViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public int Quantity { get; set; }

        public int SortWidth { get; set; }
        public int SortLength { get; set; }
    }
}
