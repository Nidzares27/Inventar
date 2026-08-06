using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Inventar.Models;
using Microsoft.EntityFrameworkCore;

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
                product.Model,
                BuildSizePart(product.Width, product.Length),
                product.Color,
                product.UnID,
                product.Id > 0 ? product.Id.ToString(CultureInfo.InvariantCulture) : null
            };

            return NormalizeSlug(string.Join("-", parts.Where(part => !string.IsNullOrWhiteSpace(part))));
        }

        public static string BuildSlugWithNumericSuffix(string baseSlug, int suffix)
        {
            var normalizedBaseSlug = NormalizeSlug(baseSlug);
            if (string.IsNullOrWhiteSpace(normalizedBaseSlug))
            {
                normalizedBaseSlug = "product";
            }

            var suffixText = suffix.ToString(CultureInfo.InvariantCulture);
            var maxBaseLength = Math.Max(1, MaxSlugLength - suffixText.Length - 1);
            if (normalizedBaseSlug.Length > maxBaseLength)
            {
                normalizedBaseSlug = normalizedBaseSlug[..maxBaseLength].Trim('-');
            }

            return $"{normalizedBaseSlug}-{suffixText}";
        }

        public static async Task<string> GenerateUniqueSlugAsync(
            IQueryable<Tepih> productsQuery,
            Tepih product,
            int? excludedProductId = null,
            string? preferredSlug = null,
            ISet<string>? reservedSlugs = null,
            CancellationToken cancellationToken = default)
        {
            var baseSlug = string.IsNullOrWhiteSpace(preferredSlug)
                ? BuildDefaultSlug(product)
                : NormalizeSlug(preferredSlug);

            if (string.IsNullOrWhiteSpace(baseSlug))
            {
                baseSlug = "product";
            }

            var candidateSlug = baseSlug;
            var suffix = 2;

            while (await SlugExistsAsync(productsQuery, candidateSlug, excludedProductId, reservedSlugs, cancellationToken))
            {
                candidateSlug = BuildSlugWithNumericSuffix(baseSlug, suffix);
                suffix++;
            }

            reservedSlugs?.Add(candidateSlug);
            return candidateSlug;
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

        private static async Task<bool> SlugExistsAsync(
            IQueryable<Tepih> productsQuery,
            string slug,
            int? excludedProductId,
            ISet<string>? reservedSlugs,
            CancellationToken cancellationToken)
        {
            if (reservedSlugs?.Contains(slug) == true)
            {
                return true;
            }

            var query = productsQuery.Where(product => product.Slug == slug);
            if (excludedProductId.HasValue)
            {
                query = query.Where(product => product.Id != excludedProductId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }
    }
}
