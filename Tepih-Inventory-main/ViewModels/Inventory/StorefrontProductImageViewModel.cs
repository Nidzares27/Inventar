namespace Inventar.ViewModels.Inventory
{
    public class StorefrontProductImageViewModel
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public string? AltText { get; set; }
        public bool IsPrimary { get; set; }
        public int SortOrder { get; set; }
    }
}
