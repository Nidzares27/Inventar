using System.Globalization;

namespace Inventar.Utils
{
    public static class LocalizationSettings
    {
        public const string DefaultCultureName = "sr-Latn";

        public static readonly string[] SupportedCultureNames =
        [
            "en",
            DefaultCultureName,
            "tr"
        ];

        public static readonly IReadOnlyList<CultureInfo> SupportedCultures =
            SupportedCultureNames.Select(CreateCultureInfo).ToArray();

        public static bool TryGetSupportedCulture(string? cultureName, out string normalizedCultureName)
        {
            normalizedCultureName = string.Empty;

            if (string.IsNullOrWhiteSpace(cultureName))
            {
                return false;
            }

            normalizedCultureName = SupportedCultureNames.FirstOrDefault(name =>
                string.Equals(name, cultureName.Trim(), StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

            return !string.IsNullOrEmpty(normalizedCultureName);
        }

        public static string GetSupportedCultureOrDefault(string? cultureName)
        {
            return TryGetSupportedCulture(cultureName, out var normalizedCultureName)
                ? normalizedCultureName
                : DefaultCultureName;
        }

        public static string? TryExtractSupportedCultureFromCookie(string? cookieValue)
        {
            if (string.IsNullOrWhiteSpace(cookieValue))
            {
                return null;
            }

            foreach (var part in cookieValue.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!part.Contains('='))
                {
                    continue;
                }

                var separatorIndex = part.IndexOf('=');
                var key = part[..separatorIndex];
                var value = part[(separatorIndex + 1)..];

                if ((key.Equals("c", StringComparison.OrdinalIgnoreCase) ||
                     key.Equals("uic", StringComparison.OrdinalIgnoreCase)) &&
                    TryGetSupportedCulture(value, out var normalizedCultureName))
                {
                    return normalizedCultureName;
                }
            }

            return TryGetSupportedCulture(cookieValue, out var legacyCultureName)
                ? legacyCultureName
                : null;
        }

        public static CultureInfo CreateCultureInfo(string cultureName)
        {
            var normalizedCultureName = GetSupportedCultureOrDefault(cultureName);
            var culture = new CultureInfo(normalizedCultureName);

            if (!string.Equals(culture.Name, DefaultCultureName, StringComparison.OrdinalIgnoreCase))
            {
                return culture;
            }

            var clonedCulture = (CultureInfo)culture.Clone();
            clonedCulture.NumberFormat.CurrencyDecimalSeparator = ".";
            clonedCulture.NumberFormat.NumberDecimalSeparator = ".";

            return clonedCulture;
        }
    }
}
