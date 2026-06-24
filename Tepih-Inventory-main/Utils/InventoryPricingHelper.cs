namespace Inventar.Utils
{
    public static class InventoryPricingHelper
    {
        public static decimal CalculateLineTotal(bool perM2, bool poMjeri, decimal rate, int? width, int? length, int quantity)
        {
            var normalizedQuantity = Math.Max(quantity, 0);
            if (normalizedQuantity == 0)
            {
                return 0m;
            }

            decimal baseAmount;
            if (poMjeri)
            {
                baseAmount = rate * CalculatePoMjeriLengthFactor(length) * normalizedQuantity;
            }
            else if (perM2)
            {
                baseAmount = rate * (PoMjeriHelper.CalculateM2Total(true, width, length, normalizedQuantity) ?? 0m);
            }
            else
            {
                baseAmount = rate * normalizedQuantity;
            }

            return Math.Round(baseAmount, 2);
        }

        public static decimal ApplyDiscount(decimal amount, int? rabat)
        {
            if (!rabat.HasValue || rabat.Value <= 0)
            {
                return Math.Round(amount, 2);
            }

            return Math.Round(amount - ((decimal)rabat.Value / 100m * amount), 2);
        }

        public static decimal CalculateDiscountedLineTotal(
            bool perM2,
            bool poMjeri,
            decimal rate,
            int? width,
            int? length,
            int quantity,
            int? rabat)
        {
            return ApplyDiscount(CalculateLineTotal(perM2, poMjeri, rate, width, length, quantity), rabat);
        }

        public static decimal CalculatePoMjeriLengthFactor(int? length)
        {
            var normalizedLength = Math.Max(length ?? 100, 100);
            return normalizedLength / 100m;
        }
    }
}
