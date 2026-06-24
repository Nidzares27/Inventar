using System.Net;
using Inventar.Models;

namespace Inventar.Utils;

public static class TextEncodingHelper
{
    public static string? Decode(string? value)
    {
        return value is null ? null : WebUtility.HtmlDecode(value);
    }

    public static string? NormalizeInput(string? value, bool trim = true)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = WebUtility.HtmlDecode(value);
        return trim ? normalized.Trim() : normalized;
    }

    public static void DecodeProductsForDisplay(IEnumerable<Tepih> products)
    {
        foreach (var product in products)
        {
            DecodeProductForDisplay(product);
        }
    }

    public static void DecodeProductForDisplay(Tepih product)
    {
        product.Name = Decode(product.Name) ?? product.Name;
        product.ProductNumber = Decode(product.ProductNumber) ?? product.ProductNumber;
        product.Model = Decode(product.Model) ?? product.Model;
        product.BroaderCategory = Decode(product.BroaderCategory);
        product.NarrowerCategory = Decode(product.NarrowerCategory);
        product.Color = Decode(product.Color) ?? product.Color;
        product.Description = Decode(product.Description);
        product.ShortDescription = Decode(product.ShortDescription);
        product.SeoTitle = Decode(product.SeoTitle);
        product.SeoDescription = Decode(product.SeoDescription);
        product.Slug = Decode(product.Slug);
        product.UnID = Decode(product.UnID);

        if (product.ProductImages is null)
        {
            return;
        }

        foreach (var image in product.ProductImages)
        {
            image.AltText = Decode(image.AltText);
        }
    }

    public static void DecodeOrderForDisplay(WebOrder order)
    {
        order.OrderNumber = Decode(order.OrderNumber) ?? order.OrderNumber;
        order.CustomerFirstName = Decode(order.CustomerFirstName) ?? order.CustomerFirstName;
        order.CustomerLastName = Decode(order.CustomerLastName) ?? order.CustomerLastName;
        order.CustomerEmail = Decode(order.CustomerEmail);
        order.CustomerPhone = Decode(order.CustomerPhone);
        order.ShippingAddressLine1 = Decode(order.ShippingAddressLine1) ?? order.ShippingAddressLine1;
        order.ShippingAddressLine2 = Decode(order.ShippingAddressLine2);
        order.ShippingCity = Decode(order.ShippingCity) ?? order.ShippingCity;
        order.ShippingPostalCode = Decode(order.ShippingPostalCode);
        order.ShippingCountry = Decode(order.ShippingCountry) ?? order.ShippingCountry;
        order.CustomerNote = Decode(order.CustomerNote);
        order.InternalNote = Decode(order.InternalNote);
    }

    public static void DecodeOrderItemsForDisplay(IEnumerable<WebOrderItem> items)
    {
        foreach (var item in items)
        {
            item.ProductName = Decode(item.ProductName) ?? item.ProductName;
            item.ProductNumber = Decode(item.ProductNumber) ?? item.ProductNumber;
            item.Model = Decode(item.Model) ?? item.Model;
            item.Color = Decode(item.Color);
            item.PrimaryImageUrl = Decode(item.PrimaryImageUrl);
        }
    }

    public static void DecodeStatusHistoryForDisplay(IEnumerable<WebOrderStatusHistory> history)
    {
        foreach (var item in history)
        {
            item.Note = Decode(item.Note);
            item.ChangedBy = Decode(item.ChangedBy);
        }
    }
}
