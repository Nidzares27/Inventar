using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels.Inventory
{
    public class PoMjeriSelectionRequestViewModel
    {
        [Range(1, int.MaxValue)]
        public int ProductId { get; set; }

        [Range(1, int.MaxValue)]
        public int CustomWidth { get; set; }

        [Range(1, int.MaxValue)]
        public int CustomLength { get; set; }
    }
}
