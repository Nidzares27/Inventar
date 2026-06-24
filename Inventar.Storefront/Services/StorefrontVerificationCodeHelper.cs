using System.Security.Cryptography;
using System.Text;

namespace Inventar.Storefront.Services;

public static class StorefrontVerificationCodeHelper
{
    public static string GenerateCode()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }

    public static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes((code ?? string.Empty).Trim()));
        return Convert.ToHexString(bytes);
    }

    public static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var parts = email.Split('@');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
        {
            return email;
        }

        var local = parts[0];
        var domain = parts[1];
        var prefixLength = Math.Min(2, local.Length);
        return $"{local[..prefixLength]}***@{domain}";
    }

    public static string NormalizeEmail(string email)
    {
        return (email ?? string.Empty).Trim().ToUpperInvariant();
    }
}
