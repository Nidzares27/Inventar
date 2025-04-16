using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels
{
    public class ReceiptViewModel
    {
        public string ProductNumber { get; set; }
        public string Name { get; set; }
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }
        [Range(1, int.MaxValue)]
        public int Length { get; set; }
        [Range(1, int.MaxValue)]
        public int Width { get; set; }
        public string Size { get; set; }
        public decimal M2PerUnit { get; set; }
        public decimal M2Total { get; set; }
        [Range(0, int.MaxValue)]
        public decimal Price { get; set; }
        public decimal PriceTotal { get; set; }
    }
}
