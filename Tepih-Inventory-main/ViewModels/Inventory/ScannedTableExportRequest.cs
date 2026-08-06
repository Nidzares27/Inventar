namespace Inventar.ViewModels.Inventory
{
    public class ScannedTableExportRequest
    {
        public string Heading { get; set; } = string.Empty;
        public string FileNameBase { get; set; } = string.Empty;
        public List<string> ColumnHeaders { get; set; } = new();
        public List<List<string>> Rows { get; set; } = new();
    }
}
