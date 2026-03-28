using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Inventar.Models;

namespace Inventar.Utils
{
    public static partial class ProductSlugHelper
    {
        private const int MaxSlugLength = 160;

        [GeneratedRegex("-{2,}", RegexOptions.Compiled)]
        private static partial Regex MultiDashRegex();

        public static string BuildDefaultSlug(Tepih product)
        {
            var parts = new List<string?>
            {
                product.Name,
                product.ProductNumber,
                BuildSizePart(product.Width, product.Length),
                product.Color,
                product.Id > 0 ? product.Id.ToString(CultureInfo.InvariantCulture) : null
            };

            return NormalizeSlug(string.Join("-", parts.Where(part => !string.IsNullOrWhiteSpace(part))));
        }

        public static string NormalizeSlug(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim().ToLowerInvariant()
                .Replace("đ", "dj")
                .Replace("š", "s")
                .Replace("č", "c")
                .Replace("ć", "c")
                .Replace("ž", "z");

            var builder = new StringBuilder(normalized.Length);
            foreach (var character in normalized.Normalize(NormalizationForm.FormD))
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
                else
                {
                    builder.Append('-');
                }
            }

            normalized = MultiDashRegex().Replace(builder.ToString(), "-").Trim('-');
            if (normalized.Length > MaxSlugLength)
            {
                normalized = normalized[..MaxSlugLength].Trim('-');
            }

            return normalized;
        }

        private static string? BuildSizePart(int? width, int? length)
        {
            return (width, length) switch
            {
                (not null, not null) => $"{width}x{length}",
                (not null, null) => width.Value.ToString(CultureInfo.InvariantCulture),
                (null, not null) => length.Value.ToString(CultureInfo.InvariantCulture),
                _ => null
            };
        }
    }
}
