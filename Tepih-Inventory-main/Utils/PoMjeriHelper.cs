using Inventar.Models;

namespace Inventar.Utils
{
    public static class PoMjeriHelper
    {
        private const string UnIdCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public const int UnIdLength = 6;

        public static string FormatSize(int? width, int? length)
        {
            return width.HasValue && length.HasValue
                ? $"{width.Value}X{length.Value}"
                : string.Empty;
        }

        public static string FormatRemainingSize(int? width, int? remainingLength)
        {
            return FormatSize(width, remainingLength);
        }

        public static string GenerateCandidateUnId()
        {
            Span<char> buffer = stackalloc char[UnIdLength];

            for (var index = 0; index < buffer.Length; index++)
            {
                buffer[index] = UnIdCharacters[Random.Shared.Next(UnIdCharacters.Length)];
            }

            return new string(buffer);
        }

        public static int CalculateConsumedLengthPerUnit(int remainingWidth, int customWidth, int customLength)
        {
            if (remainingWidth <= 0 || customWidth <= 0 || customLength <= 0)
            {
                return 0;
            }

            // If the longer side can fit into the roll width, we rotate the cut so
            // the shorter side becomes the consumed length.
            return customLength <= remainingWidth
                ? customWidth
                : customLength;
        }

        public static int CalculateMaxAvailableQuantity(int remainingWidth, int remainingLength, int customWidth, int customLength)
        {
            if (remainingWidth <= 0 || remainingLength <= 0)
            {
                return 0;
            }

            var consumedLengthPerUnit = CalculateConsumedLengthPerUnit(remainingWidth, customWidth, customLength);
            return consumedLengthPerUnit <= 0
                ? 0
                : Math.Max(remainingLength / consumedLengthPerUnit, 0);
        }

        public static int CalculateRemainingLength(Tepih product, IEnumerable<Prodaja>? sales)
        {
            if (!product.PoMjeri || !product.Length.HasValue)
            {
                return product.Length ?? 0;
            }

            var soldLength = sales?
                .Where(sale => !sale.Disabled)
                .Sum(GetConsumedLengthTotal) ?? 0;

            return Math.Max(product.Length.Value - soldLength, 0);
        }

        public static int GetConsumedLengthTotal(Prodaja sale)
        {
            if (sale == null)
            {
                return 0;
            }

            var perUnit = sale.ConsumedLength
                ?? sale.CustomLength
                ?? 0;

            return Math.Max(perUnit, 0) * Math.Max(sale.Quantity, 0);
        }

        public static int? GetEffectiveWidth(Tepih product, Prodaja sale)
        {
            return sale.CustomWidth ?? product.Width;
        }

        public static int? GetEffectiveLength(Tepih product, Prodaja sale)
        {
            return sale.CustomLength ?? product.Length;
        }

        public static decimal? CalculateM2PerUnit(bool perM2, int? width, int? length)
        {
            if (!perM2 || !width.HasValue || !length.HasValue)
            {
                return null;
            }

            return Math.Round((decimal)width.Value * length.Value / 10000m, 2);
        }

        public static decimal? CalculateM2Total(bool perM2, int? width, int? length, int quantity)
        {
            var perUnit = CalculateM2PerUnit(perM2, width, length);
            return perUnit.HasValue
                ? Math.Round(perUnit.Value * quantity, 2)
                : null;
        }

        public static int? GetInventoryDisplayLength(Tepih product, IReadOnlyDictionary<int, int>? remainingLengths = null)
        {
            if (!product.PoMjeri)
            {
                return product.Length;
            }

            if (remainingLengths != null && remainingLengths.TryGetValue(product.Id, out var remainingLength))
            {
                return remainingLength;
            }

            return product.Length;
        }

        public static decimal? CalculateInventoryDisplayM2PerUnit(Tepih product, IReadOnlyDictionary<int, int>? remainingLengths = null)
        {
            return CalculateM2PerUnit(product.PerM2, product.Width, GetInventoryDisplayLength(product, remainingLengths));
        }

        public static decimal? CalculateInventoryDisplayM2Total(Tepih product, IReadOnlyDictionary<int, int>? remainingLengths = null)
        {
            return CalculateM2Total(product.PerM2, product.Width, GetInventoryDisplayLength(product, remainingLengths), product.Quantity);
        }
    }
}
