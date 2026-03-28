using Inventar.Storefront.Models;
using Inventar.Storefront.ViewModels.Cart;
using Inventar.Storefront.ViewModels.Catalog;

namespace Inventar.Storefront.Services;

public static class StorefrontViewModelMapper
{
    public static ProductCardViewModel ToProductCard(StorefrontProduct product)
    {
        return new ProductCardViewModel
        {
            Id = product.Id,
            Slug = product.Slug ?? product.Id.ToString(),
            Name = product.Name,
            //Name = string.IsNullOrWhiteSpace(product.Model) ? product.Name : product.Model,
            Model = product.Model,
            ProductNumber = product.ProductNumber,
            CollectionName = BuildCollectionName(product),
            Color = product.Color,
            SizeLabel = BuildSizeLabel(product.Width, product.Length),
            Description = BuildShortDescription(product),
            ImageUrl = BuildPrimaryImageUrl(product),
            CurrentPrice = product.EffectivePrice,
            CompareAtPrice = product.OnlinePrice.HasValue && product.OnlinePrice.Value < product.Price ? product.Price : null,
            AvailableQuantity = product.AvailableQuantity
        };
    }

    public static CartLineViewModel ToCartLine(StorefrontProduct product, int quantity)
    {
        return new CartLineViewModel
        {
            ProductId = product.Id,
            Slug = product.Slug ?? product.Id.ToString(),
            Name = product.Name,
            //Name = string.IsNullOrWhiteSpace(product.Model) ? product.Name : product.Model,
            Model = product.Model,
            ProductNumber = product.ProductNumber,
            SizeLabel = BuildSizeLabel(product.Width, product.Length),
            Color = product.Color,
            ImageUrl = BuildPrimaryImageUrl(product),
            Quantity = quantity,
            AvailableQuantity = product.AvailableQuantity,
            UnitPrice = product.EffectivePrice,
            LineTotal = product.EffectivePrice * quantity
        };
    }

    public static CartAddedNotificationViewModel ToCartAddedNotification(StorefrontProduct product, int quantityAdded)
    {
        return new CartAddedNotificationViewModel
        {
            Name = product.Name,
            CollectionName = product.Model,
            //Name = string.IsNullOrWhiteSpace(product.Model) ? product.Name : product.Model,
            //CollectionName = BuildCollectionName(product),
            SizeLabel = BuildSizeLabel(product.Width, product.Length),
            Color = product.Color,
            ImageUrl = BuildPrimaryImageUrl(product),
            QuantityAdded = quantityAdded,
            UnitPrice = product.EffectivePrice
        };
    }

    public static string BuildCollectionName(StorefrontProduct product)
    {
        return string.IsNullOrWhiteSpace(product.Name) ? "Studio izbor" : product.Name.Trim();
    }

    public static string BuildShortDescription(StorefrontProduct product)
    {
        var source = !string.IsNullOrWhiteSpace(product.ShortDescription)
            ? product.ShortDescription
            : product.Description;

        if (string.IsNullOrWhiteSpace(source))
        {
            return "";
            //return "Topla tekstura, odabrana boja i dimenzija spremna za vaš prostor.";

        }

        return source.Length <= 120 ? source : $"{source[..117]}...";
    }

    public static string BuildDescription(StorefrontProduct product)
    {
        if (!string.IsNullOrWhiteSpace(product.Description))
        {
            return product.Description.Trim();
        }

        var size = BuildSizeLabel(product.Width, product.Length);
        return $"Model {product.Model} iz kolekcije {BuildCollectionName(product)} u boji {product.Color}{(string.IsNullOrWhiteSpace(size) ? string.Empty : $" i dimenziji {size}")}.";
    }

    public static string BuildSizeLabel(int? width, int? length)
    {
        return width.HasValue && length.HasValue
            ? $"{width.Value} x {length.Value} cm"
            : string.Empty;
    }

    private static string? BuildPrimaryImageUrl(StorefrontProduct product)
    {
        return product.ProductImages
            .Where(image => !image.Disabled)
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .Select(image => string.IsNullOrWhiteSpace(image.ThumbnailUrl) ? image.Url : image.ThumbnailUrl)
            .FirstOrDefault();
    }
}
