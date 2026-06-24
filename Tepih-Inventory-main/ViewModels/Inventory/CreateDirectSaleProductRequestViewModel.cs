using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels.Inventory
{
    public class CreateDirectSaleProductRequestViewModel
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Range(0, 999999999)]
        public decimal Price { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Range(1, int.MaxValue)]
        public int? Width { get; set; }

        [Range(1, int.MaxValue)]
        public int? Length { get; set; }

        [Required]
        [StringLength(20)]
        public string ProductType { get; set; } = "perUnit";
    }
}
