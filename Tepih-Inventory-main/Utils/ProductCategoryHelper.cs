namespace Inventar.Utils
{
    public static class ProductCategoryHelper
    {
        public const string PlaceholderCategory = "default";

        public static string Normalize(string? category)
        {
            return string.IsNullOrWhiteSpace(category)
                ? PlaceholderCategory
                : category.Trim();
        }

        public static bool IsPlaceholder(string? category)
        {
            return string.IsNullOrWhiteSpace(category)
                || string.Equals(category.Trim(), PlaceholderCategory, StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldUpgradePlaceholder(string? existingCategory, string? submittedCategory)
        {
            return IsPlaceholder(existingCategory) && !IsPlaceholder(submittedCategory);
        }
    }
}
