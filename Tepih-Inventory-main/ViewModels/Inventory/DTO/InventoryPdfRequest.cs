namespace Inventar.ViewModels.Inventory.DTO
{
    public class InventoryPdfRequest
    {
        public string Heading { get; set; }
        public List<string[]> Data { get; set; }
        public Dictionary<int, string> Filters { get; set; }
        public List<string> ColumnHeaders { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalM2 { get; set; }
    }
}
