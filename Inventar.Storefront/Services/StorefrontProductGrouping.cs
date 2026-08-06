using Inventar.Storefront.Models;
using Inventar.Storefront.Utils;
using Inventar.Storefront.ViewModels.Catalog;

namespace Inventar.Storefront.Services;

public static class StorefrontProductGrouping
{
    public static IReadOnlyList<GroupedStorefrontProduct> GroupVariants(
        IEnumerable<StorefrontProduct> products,
        IReadOnlyDictionary<int, int>? effectiveAvailability = null)
    {
        return products
            .GroupBy(StorefrontPoMjeriPlanner.BuildGroupKey)
            .Select(group => new GroupedStorefrontProduct(group.ToList(), effectiveAvailability))
            .ToList();
    }

    public static IReadOnlyList<GroupedStorefrontProduct> SortGroups(IEnumerable<GroupedStorefrontProduct> groups, string sort)
    {
        return sort switch
        {
            "newest-desc" or "featured" => groups
                .OrderBy(group => group.TotalAvailableQuantity <= 0)
                .ThenByDescending(group => group.LatestProductId)
                .ToList(),
            "newest-asc" => groups
                .OrderBy(group => group.TotalAvailableQuantity <= 0)
                .ThenBy(group => group.LatestProductId)
                .ToList(),
            "price-asc" => groups
                .OrderBy(group => group.TotalAvailableQuantity <= 0)
                .ThenBy(group => group.RepresentativeProduct.EffectivePrice)
                .ThenBy(group => group.RepresentativeProduct.Name)
                .ThenBy(group => group.RepresentativeProduct.Model)
                .ToList(),
            "price-desc" => groups
                .OrderBy(group => group.TotalAvailableQuantity <= 0)
                .ThenByDescending(group => group.RepresentativeProduct.EffectivePrice)
                .ThenBy(group => group.RepresentativeProduct.Name)
                .ThenBy(group => group.RepresentativeProduct.Model)
                .ToList(),
            "name-asc" or "name" => groups
                .OrderBy(group => group.TotalAvailableQuantity <= 0)
                .ThenBy(group => group.RepresentativeProduct.Name)
                .ThenBy(group => group.RepresentativeProduct.Model)
                .ThenBy(group => group.RepresentativeProduct.Id)
                .ToList(),
            "name-desc" => groups
                .OrderBy(group => group.TotalAvailableQuantity <= 0)
                .ThenByDescending(group => group.RepresentativeProduct.Name)
                .ThenByDescending(group => group.RepresentativeProduct.Model)
                .ThenByDescending(group => group.RepresentativeProduct.Id)
                .ToList(),
            _ => groups
                .OrderBy(group => group.TotalAvailableQuantity <= 0)
                .ThenByDescending(group => group.LatestProductId)
                .ToList()
        };
    }

    public static IReadOnlyList<ProductGalleryImageViewModel> BuildDistinctGalleryImages(IEnumerable<StorefrontProduct> products)
    {
        var images = new List<ProductGalleryImageViewModel>();
        var seenImages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var product in products)
        {
            foreach (var image in product.ProductImages
                         .Where(image =>
                             !image.Disabled &&
                             ProductMediaFolders.IsStorefrontMedia(image.CloudinaryPublicId) &&
                             !string.IsNullOrWhiteSpace(image.Url))
                         .OrderByDescending(image => image.IsPrimary)
                         .ThenBy(image => image.SortOrder)
                         .ThenBy(image => image.Id))
            {
                if (!seenImages.Add(BuildGalleryImageKey(image)))
                {
                    continue;
                }

                images.Add(new ProductGalleryImageViewModel
                {
                    Url = image.Url,
                    ThumbnailUrl = NormalizeMediaType(image.MediaType) == "video"
                        ? null
                        : (string.IsNullOrWhiteSpace(image.ThumbnailUrl) ? image.Url : image.ThumbnailUrl),
                    MediaType = NormalizeMediaType(image.MediaType),
                    AltText = string.IsNullOrWhiteSpace(image.AltText)
                        ? $"{product.Name} - {product.Model}"
                        : image.AltText.Trim()
                });
            }
        }

        return images;
    }

    private static string BuildGalleryImageKey(ProductImage image)
    {
        var mediaType = NormalizeMediaType(image.MediaType);
        if (!string.IsNullOrWhiteSpace(image.CloudinaryPublicId))
        {
            return $"{mediaType}:{image.CloudinaryPublicId.Trim()}";
        }

        return $"{mediaType}:{image.Url.Trim()}";
    }

    private static string NormalizeMediaType(string? mediaType)
    {
        return string.Equals(mediaType, "video", StringComparison.OrdinalIgnoreCase)
            ? "video"
            : "image";
    }

}

public sealed class GroupedStorefrontProduct
{
    public GroupedStorefrontProduct(
        IReadOnlyList<StorefrontProduct> variants,
        IReadOnlyDictionary<int, int>? effectiveAvailability = null)
    {
        EffectiveAvailability = effectiveAvailability ?? new Dictionary<int, int>();
        Variants = variants
            .OrderByDescending(product => GetAvailableQuantity(product) > 0)
            .ThenBy(product => product.Id)
            .ToList();

        RepresentativeProduct = Variants[0];
        LatestProductId = Variants.Max(product => product.Id);
        TotalAvailableQuantity = Variants.Sum(GetAvailableQuantity);
        HasPoMjeriVariants = Variants.Any(product => product.PoMjeri);
        HasSelectableOptions = HasPoMjeriVariants || HasMultipleDistinctValues(
                Variants.Select(product => product.Color),
                StringComparer.OrdinalIgnoreCase) ||
            HasMultipleDistinctValues(
                Variants.Select(product => BuildSizeLabel(product)),
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<StorefrontProduct> Variants { get; }
    public StorefrontProduct RepresentativeProduct { get; }
    public int LatestProductId { get; }
    public int TotalAvailableQuantity { get; }
    public bool HasSelectableOptions { get; }
    public bool HasPoMjeriVariants { get; }
    public IReadOnlyDictionary<int, int> EffectiveAvailability { get; }

    public int GetAvailableQuantity(StorefrontProduct product)
    {
        return EffectiveAvailability.TryGetValue(product.Id, out var quantity)
            ? quantity
            : product.AvailableQuantity;
    }

    private static bool HasMultipleDistinctValues(IEnumerable<string?> values, IEqualityComparer<string> comparer)
    {
        var distinctValues = new HashSet<string>(comparer);

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            distinctValues.Add(value.Trim());
            if (distinctValues.Count > 1)
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildSizeLabel(StorefrontProduct product)
    {
        return product.Width.HasValue && product.Length.HasValue
            ? $"{product.Width.Value} x {product.Length.Value} cm"
            : string.Empty;
    }
}
