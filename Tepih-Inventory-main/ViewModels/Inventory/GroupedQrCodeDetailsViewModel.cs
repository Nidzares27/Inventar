using Inventar.Models;

namespace Inventar.ViewModels.Inventory
{
    public class GroupedQrCodeDetailsViewModel
    {
        public string ProductNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public List<Tepih> Products { get; set; } = new();
    }
}
