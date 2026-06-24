namespace Inventar.ViewModels.Inventory
{
    public class GroupedQrCodeRowViewModel
    {
        public string RawProductNumber { get; set; } = string.Empty;
        public string RawName { get; set; } = string.Empty;
        public string RawModel { get; set; } = string.Empty;
        public string RawColor { get; set; } = string.Empty;

        public string ProductNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;

        public int TotalQuantity { get; set; }
        public bool IsPoMjeriGroup { get; set; }
    }
}
