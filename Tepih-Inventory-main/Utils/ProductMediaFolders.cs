namespace Inventar.Utils
{
    public static class ProductMediaFolders
    {
        public const string InventoryGalleryFolder = "Galerija";
        public const string InventoryGalleryFolderPrefix = InventoryGalleryFolder + "/";
        public const string StorefrontFolder = "StorefrontProducts";
        public const string StorefrontFolderPrefix = StorefrontFolder + "/";

        public static bool IsInventoryGalleryMedia(string? cloudinaryPublicId)
        {
            return !string.IsNullOrWhiteSpace(cloudinaryPublicId) &&
                   cloudinaryPublicId.StartsWith(InventoryGalleryFolderPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsStorefrontMedia(string? cloudinaryPublicId)
        {
            return !IsInventoryGalleryMedia(cloudinaryPublicId);
        }
    }
}
