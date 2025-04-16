namespace Inventar.ViewModels
{
    public class PerProductViewModel
    {
        public string CustomerFullName { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public List<SalesReportViewModel> SalesReport { get; set; }
    }
}
