namespace Inventar.Storefront.Services;

public static class StorefrontCategoryHelper
{
    public const string PlaceholderCategory = "default";

    public static string Normalize(string? category)
    {
        return string.IsNullOrWhiteSpace(category)
            ? string.Empty
            : category.Trim();
    }

    public static bool IsMeaningful(string? category)
    {
        return !string.IsNullOrWhiteSpace(category)
            && !string.Equals(category.Trim(), PlaceholderCategory, StringComparison.OrdinalIgnoreCase);
    }

    public static bool Matches(string? left, string? right)
    {
        return IsMeaningful(left)
            && IsMeaningful(right)
            && string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
