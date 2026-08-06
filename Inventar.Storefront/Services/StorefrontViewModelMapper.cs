using Inventar.Storefront.Models;
using Inventar.Storefront.Utils;
using Inventar.Storefront.ViewModels.Cart;
using Inventar.Storefront.ViewModels.Catalog;

namespace Inventar.Storefront.Services;

public static class StorefrontViewModelMapper
{
    public static ProductCardViewModel ToProductCard(GroupedStorefrontProduct group)
    {
        var product = group.RepresentativeProduct;

        return new ProductCardViewModel
        {
            Id = product.Id,
            DirectAddProductId = product.Id,
            Slug = product.Slug ?? product.Id.ToString(),
            Name = TextEncodingHelper.Decode(product.Name) ?? product.Name,
            Model = TextEncodingHelper.Decode(product.Model) ?? product.Model,
            ProductNumber = TextEncodingHelper.Decode(product.ProductNumber) ?? product.ProductNumber,
            CollectionName = BuildCollectionName(product),
            Description = BuildShortDescription(product),
            ImageUrl = BuildPrimaryImageUrl(product) ??
                group.Variants
                    .Select(BuildPrimaryImageUrl)
                    .FirstOrDefault(imageUrl => !string.IsNullOrWhiteSpace(imageUrl)),
            CurrentPrice = product.EffectivePrice,
            CompareAtPrice = product.OnlinePrice.HasValue && product.OnlinePrice.Value < product.Price ? product.Price : null,
            AvailableQuantity = group.TotalAvailableQuantity,
            HasSelectableOptions = group.HasSelectableOptions,
            PoMjeri = group.HasPoMjeriVariants,
            IsSoldOut = group.TotalAvailableQuantity <= 0,
            ShowSoldOutOverlay = !group.HasSelectableOptions && group.TotalAvailableQuantity <= 0,
            CanAddToCart = !group.HasSelectableOptions && group.TotalAvailableQuantity > 0
        };
    }

    public static ProductCardViewModel ToProductCard(StorefrontProduct product)
    {
        return new ProductCardViewModel
        {
            Id = product.Id,
            DirectAddProductId = product.Id,
            Slug = product.Slug ?? product.Id.ToString(),
            Name = TextEncodingHelper.Decode(product.Name) ?? product.Name,
            //Name = string.IsNullOrWhiteSpace(product.Model) ? product.Name : product.Model,
            Model = TextEncodingHelper.Decode(product.Model) ?? product.Model,
            ProductNumber = TextEncodingHelper.Decode(product.ProductNumber) ?? product.ProductNumber,
            CollectionName = BuildCollectionName(product),
            Description = BuildShortDescription(product),
            ImageUrl = BuildPrimaryImageUrl(product),
            CurrentPrice = product.EffectivePrice,
            CompareAtPrice = product.OnlinePrice.HasValue && product.OnlinePrice.Value < product.Price ? product.Price : null,
            AvailableQuantity = product.AvailableQuantity,
            HasSelectableOptions = product.PoMjeri,
            PoMjeri = product.PoMjeri,
            IsSoldOut = product.AvailableQuantity <= 0,
            ShowSoldOutOverlay = !product.PoMjeri && product.AvailableQuantity <= 0,
            CanAddToCart = !product.PoMjeri && product.AvailableQuantity > 0
        };
    }

    public static CartLineViewModel ToCartLine(
        StorefrontProduct product,
        CartItem cartItem,
        int maxOrderQuantity,
        StorefrontPricingResult? pricing = null)
    {
        var shortDescription = BuildShortDescriptionText(product);
        var width = cartItem.PoMjeri ? cartItem.CustomWidth : product.Width;
        var length = cartItem.PoMjeri ? cartItem.CustomLength : product.Length;
        var resolvedPricing = pricing ?? (cartItem.PoMjeri
            ? StorefrontPricingHelper.BuildPoMjeriPricing(product.EffectivePrice, product.Price, length)
            : StorefrontPricingHelper.BuildPricing(product, width, length));

        return new CartLineViewModel
        {
            LineId = cartItem.LineId,
            ProductId = product.Id,
            Slug = product.Slug ?? product.Id.ToString(),
            Name = TextEncodingHelper.Decode(product.Name) ?? product.Name,
            ShortDescription = shortDescription,
            //Name = string.IsNullOrWhiteSpace(product.Model) ? product.Name : product.Model,
            Model = TextEncodingHelper.Decode(product.Model) ?? product.Model,
            ProductNumber = TextEncodingHelper.Decode(product.ProductNumber) ?? product.ProductNumber,
            SizeLabel = BuildSizeLabel(width, length),
            Color = TextEncodingHelper.Decode(string.IsNullOrWhiteSpace(cartItem.SelectedColor) ? product.Color : cartItem.SelectedColor),
            ImageUrl = BuildPrimaryImageUrl(product),
            Quantity = cartItem.Quantity,
            AvailableQuantity = maxOrderQuantity,
            MaxOrderQuantity = maxOrderQuantity,
            PoMjeri = cartItem.PoMjeri,
            PerM2 = product.PerM2,
            UnitPrice = resolvedPricing.UnitPrice,
            LineTotal = resolvedPricing.UnitPrice * cartItem.Quantity,
            PricePerSquareMeter = resolvedPricing.PricePerSquareMeter
        };
    }

    public static CartAddedNotificationViewModel ToCartAddedNotification(
        StorefrontProduct product,
        int quantityAdded,
        int? customWidth = null,
        int? customLength = null,
        string? selectedColor = null,
        StorefrontPricingResult? pricing = null)
    {
        var shortDescription = BuildShortDescriptionText(product);
        var width = customWidth ?? product.Width;
        var length = customLength ?? product.Length;
        var resolvedPricing = pricing ?? StorefrontPricingHelper.BuildPricing(product, width, length);

        return new CartAddedNotificationViewModel
        {
            Name = BuildProductTitle(product),
            ShortDescription = shortDescription,
            CollectionName = BuildCollectionName(product),
            SizeLabel = BuildSizeLabel(width, length),
            Color = TextEncodingHelper.Decode(selectedColor ?? product.Color) ?? (selectedColor ?? product.Color),
            ImageUrl = BuildPrimaryImageUrl(product),
            QuantityAdded = quantityAdded,
            UnitPrice = resolvedPricing.UnitPrice,
            PricePerSquareMeter = resolvedPricing.PricePerSquareMeter
        };
    }

    public static string BuildCollectionName(StorefrontProduct product)
    {
        var broaderCategory = TextEncodingHelper.Decode(StorefrontCategoryHelper.Normalize(product.BroaderCategory));
        var narrowerCategory = TextEncodingHelper.Decode(StorefrontCategoryHelper.Normalize(product.NarrowerCategory));

        if (StorefrontCategoryHelper.IsMeaningful(narrowerCategory) && StorefrontCategoryHelper.IsMeaningful(broaderCategory))
        {
            return $"{broaderCategory} / {narrowerCategory}";
        }

        if (StorefrontCategoryHelper.IsMeaningful(narrowerCategory))
        {
            return narrowerCategory;
        }

        if (StorefrontCategoryHelper.IsMeaningful(broaderCategory))
        {
            return broaderCategory;
        }

        var name = TextEncodingHelper.Decode(product.Name);
        return string.IsNullOrWhiteSpace(name) ? "Studio izbor" : name.Trim();
    }

    public static string BuildShortDescriptionText(StorefrontProduct product)
    {
        var source = !string.IsNullOrWhiteSpace(product.ShortDescription)
            ? product.ShortDescription
            : product.Description;
        source = TextEncodingHelper.Decode(source);

        if (string.IsNullOrWhiteSpace(source))
        {
            return BuildProductTitle(product);
        }

        return source.Trim();
    }

    public static string BuildShortDescription(StorefrontProduct product)
    {
        var source = !string.IsNullOrWhiteSpace(product.ShortDescription)
            ? product.ShortDescription
            : product.Description;
        source = TextEncodingHelper.Decode(source);

        if (string.IsNullOrWhiteSpace(source))
        {
            return "";
            //return "Topla tekstura, odabrana boja i dimenzija spremna za vaš prostor.";

        }

        return source.Length <= 120 ? source : $"{source[..117]}...";
    }

    public static string BuildDescription(StorefrontProduct product)
    {
        var description = TextEncodingHelper.Decode(product.Description);
        if (!string.IsNullOrWhiteSpace(description))
        {
            return description.Trim();
        }

        var size = BuildSizeLabel(product.Width, product.Length);
        var model = TextEncodingHelper.Decode(product.Model) ?? product.Model;
        var color = TextEncodingHelper.Decode(product.Color) ?? product.Color;
        return $"Model {model} iz kategorije {BuildCollectionName(product)} u boji {color}{(string.IsNullOrWhiteSpace(size) ? string.Empty : $" i dimenziji {size}")}.";
    }

    public static string BuildSizeLabel(int? width, int? length)
    {
        return width.HasValue && length.HasValue
            ? $"{width.Value} x {length.Value} cm"
            : string.Empty;
    }

    public static string BuildProductTitle(StorefrontProduct product)
    {
        var name = TextEncodingHelper.Decode(product.Name) ?? product.Name;
        var model = TextEncodingHelper.Decode(product.Model) ?? product.Model;
        return string.IsNullOrWhiteSpace(model)
            ? name
            : $"{name} - {model}";
    }

    public static string? BuildPrimaryImageUrl(StorefrontProduct product)
    {
        return product.ProductImages
            .Where(image =>
                !image.Disabled &&
                ProductMediaFolders.IsStorefrontMedia(image.CloudinaryPublicId) &&
                !IsVideoMediaType(image.MediaType))
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .Select(image => string.IsNullOrWhiteSpace(image.ThumbnailUrl) ? image.Url : image.ThumbnailUrl)
            .FirstOrDefault();
    }

    public static string? BuildPrimaryGalleryImageUrl(StorefrontProduct product)
    {
        return product.ProductImages
            .Where(image => !image.Disabled && ProductMediaFolders.IsStorefrontMedia(image.CloudinaryPublicId))
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .Select(image => image.Url)
            .FirstOrDefault();
    }

    private static bool IsVideoMediaType(string? mediaType)
    {
        return string.Equals(mediaType, "video", StringComparison.OrdinalIgnoreCase);
    }
}
