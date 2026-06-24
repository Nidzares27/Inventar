using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels.Sales
{
    public class ReplaceSaleProductRequestViewModel
    {
        [Range(1, int.MaxValue)]
        public int SaleId { get; set; }

        [Range(1, int.MaxValue)]
        public int ProductId { get; set; }

        [Range(1, int.MaxValue)]
        public int? CustomWidth { get; set; }

        [Range(1, int.MaxValue)]
        public int? CustomLength { get; set; }
    }
}
