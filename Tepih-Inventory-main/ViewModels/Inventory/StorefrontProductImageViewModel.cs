namespace Inventar.ViewModels.Inventory
{
    public class StorefrontProductImageViewModel
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public string? AltText { get; set; }
        public string MediaType { get; set; } = "image";
        public bool IsPrimary { get; set; }
        public int SortOrder { get; set; }
        public bool IsVideo => string.Equals(MediaType, "video", StringComparison.OrdinalIgnoreCase);
    }
}
