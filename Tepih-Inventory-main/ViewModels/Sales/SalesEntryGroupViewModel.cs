namespace Inventar.ViewModels.Sales
{
    public class SalesEntryGroupViewModel
    {
        public bool Grouped { get; set; }
        public List<SalesEntryViewModel> Entries { get; set; }
        public LabelsViewModel Labels { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}