using System.Net;

namespace Inventar.Storefront.Utils;

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
}
