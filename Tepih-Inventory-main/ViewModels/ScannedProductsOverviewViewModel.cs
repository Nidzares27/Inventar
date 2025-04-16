namespace Inventar.ViewModels
{
    public class ScannedProductsOverviewViewModel
    {
        public string FullName { get; set; }
        public decimal AmountPaid { get; set; }
        public bool PrintPDF { get; set; }
        public DateTime PurchaseTime { get; set; }
        public List<ScannedProductViewModel>? Products { get; set; }
    }
}
