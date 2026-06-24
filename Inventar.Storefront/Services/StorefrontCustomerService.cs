using System.Security.Claims;
using Inventar.Storefront.Data;
using Inventar.Storefront.Models;
using Inventar.Storefront.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Inventar.Storefront.Services;

public class StorefrontCustomerService : IStorefrontCustomerService
{
    private readonly StorefrontDbContext _dbContext;
    private readonly StorefrontSettings _settings;
    private readonly StorefrontEmailSettings _emailSettings;

    public StorefrontCustomerService(
        StorefrontDbContext dbContext,
        IOptions<StorefrontSettings> settings,
        IOptions<StorefrontEmailSettings> emailSettings)
    {
        _dbContext = dbContext;
        _settings = settings.Value;
        _emailSettings = emailSettings.Value;
    }

    public async Task<StorefrontCustomer?> GetCurrentCustomerAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var customerIdValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(customerIdValue, out var customerId))
        {
            return null;
        }

        return await _dbContext.StorefrontCustomers
            .FirstOrDefaultAsync(customer => customer.Id == customerId && !customer.Disabled, cancellationToken);
    }

    public async Task<IssuedLoginCodeResult> IssueLoginCodeAsync(
        string email,
        bool rememberMe,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = StorefrontVerificationCodeHelper.NormalizeEmail(email);
        var utcNow = DateTime.UtcNow;
        var verificationCode = StorefrontVerificationCodeHelper.GenerateCode();

        var activeCodes = await _dbContext.StorefrontLoginCodes
            .Where(code =>
                code.NormalizedEmail == normalizedEmail &&
                code.Purpose == StorefrontLoginCodePurposes.AccountLogin &&
                code.UsedUtc == null &&
                code.ExpiresUtc > utcNow)
            .ToListAsync(cancellationToken);

        foreach (var activeCode in activeCodes)
        {
            activeCode.UsedUtc = utcNow;
        }

        var loginCode = new StorefrontLoginCode
        {
            Email = TextEncodingHelper.NormalizeInput(email) ?? string.Empty,
            NormalizedEmail = normalizedEmail,
            Purpose = StorefrontLoginCodePurposes.AccountLogin,
            CodeHash = StorefrontVerificationCodeHelper.HashCode(verificationCode),
            RememberMe = rememberMe,
            CreatedUtc = utcNow,
            ExpiresUtc = utcNow.AddMinutes(_emailSettings.VerificationCodeLifetimeMinutes)
        };

        _dbContext.StorefrontLoginCodes.Add(loginCode);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new IssuedLoginCodeResult
        {
            Email = loginCode.Email,
            VerificationCode = verificationCode,
            ExpiresUtc = loginCode.ExpiresUtc
        };
    }

    public async Task<VerifiedLoginCodeResult> VerifyLoginCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = StorefrontVerificationCodeHelper.NormalizeEmail(email);
        var utcNow = DateTime.UtcNow;

        var loginCode = await _dbContext.StorefrontLoginCodes
            .Where(item =>
                item.NormalizedEmail == normalizedEmail &&
                item.Purpose == StorefrontLoginCodePurposes.AccountLogin &&
                item.UsedUtc == null)
            .OrderByDescending(item => item.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (loginCode is null || loginCode.ExpiresUtc <= utcNow)
        {
            return Failure("Kod je istekao. Pošaljite novi.");
        }

        if (loginCode.FailedAttemptCount >= _settings.MaxLoginCodeAttempts)
        {
            return Failure("Previše neuspjesnih pokušaja. Pošaljite novi kod.");
        }

        if (!string.Equals(
                loginCode.CodeHash,
                StorefrontVerificationCodeHelper.HashCode(code),
                StringComparison.Ordinal))
        {
            loginCode.FailedAttemptCount += 1;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Failure("Kod nije ispravan.");
        }

        loginCode.UsedUtc = utcNow;

        var customer = await GetOrCreateByVerifiedEmailAsync(
            loginCode.Email,
            cancellationToken: cancellationToken);
        customer.LastLoginUtc = utcNow;
        customer.EmailVerifiedUtc ??= utcNow;

        await LinkOrdersByEmailAsync(customer, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new VerifiedLoginCodeResult
        {
            Succeeded = true,
            RememberMe = loginCode.RememberMe,
            Customer = customer
        };
    }

    public async Task<StorefrontCustomer> GetOrCreateByVerifiedEmailAsync(
        string email,
        StorefrontVerifiedIdentityData? identity = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = StorefrontVerificationCodeHelper.NormalizeEmail(email);
        var customer = await _dbContext.StorefrontCustomers
            .FirstOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken);

        if (customer is not null)
        {
            customer.Email = TextEncodingHelper.NormalizeInput(email) ?? string.Empty;
            customer.NormalizedEmail = normalizedEmail;
            ApplyVerifiedIdentity(customer, identity);
            customer.UpdatedUtc = DateTime.UtcNow;
            return customer;
        }

        customer = new StorefrontCustomer
        {
            Email = TextEncodingHelper.NormalizeInput(email) ?? string.Empty,
            NormalizedEmail = normalizedEmail,
            Country = "Crna Gora",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            EmailVerifiedUtc = DateTime.UtcNow
        };

        ApplyVerifiedIdentity(customer, identity);
        _dbContext.StorefrontCustomers.Add(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return customer;
    }

    public async Task LinkOrdersByEmailAsync(StorefrontCustomer customer, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = customer.NormalizedEmail;
        var matchingOrders = await _dbContext.WebOrders
            .Where(order =>
                order.StorefrontCustomerId == null &&
                order.CustomerEmail != null &&
                order.CustomerEmail.ToUpper() == normalizedEmail)
            .ToListAsync(cancellationToken);

        foreach (var matchingOrder in matchingOrders)
        {
            matchingOrder.StorefrontCustomerId = customer.Id;
        }
    }

    public async Task SaveProfileAsync(
        StorefrontCustomer customer,
        StorefrontCustomerProfileData profile,
        CancellationToken cancellationToken = default)
    {
        customer.FirstName = NormalizeOptionalValue(profile.FirstName, 50);
        customer.LastName = NormalizeOptionalValue(profile.LastName, 50);
        customer.Phone = NormalizeOptionalValue(profile.Phone, 30);
        customer.AddressLine1 = NormalizeOptionalValue(profile.AddressLine1, 200);
        customer.AddressLine2 = NormalizeOptionalValue(profile.AddressLine2, 200);
        customer.City = NormalizeOptionalValue(profile.City, 100);
        customer.PostalCode = NormalizeOptionalValue(profile.PostalCode, 20);
        customer.Country = NormalizeOptionalValue(profile.Country, 100) ?? "Crna Gora";
        customer.UpdatedUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public ClaimsPrincipal CreatePrincipal(StorefrontCustomer customer)
    {
        var displayName = string.IsNullOrWhiteSpace(customer.DisplayName)
            ? customer.Email
            : customer.DisplayName;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, customer.Id.ToString()),
            new(ClaimTypes.Email, customer.Email),
            new(ClaimTypes.Name, displayName)
        };

        if (!string.IsNullOrWhiteSpace(customer.FirstName))
        {
            claims.Add(new Claim(ClaimTypes.GivenName, customer.FirstName));
        }

        if (!string.IsNullOrWhiteSpace(customer.LastName))
        {
            claims.Add(new Claim(ClaimTypes.Surname, customer.LastName));
        }

        var identity = new ClaimsIdentity(claims, StorefrontAuthenticationConstants.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    private static VerifiedLoginCodeResult Failure(string message)
    {
        return new VerifiedLoginCodeResult
        {
            Succeeded = false,
            Message = message
        };
    }

    private static string? NormalizeOptionalValue(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = TextEncodingHelper.NormalizeInput(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }

    private static void ApplyVerifiedIdentity(StorefrontCustomer customer, StorefrontVerifiedIdentityData? identity)
    {
        if (identity is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(customer.FirstName))
        {
            customer.FirstName = NormalizeOptionalValue(identity.FirstName, 50);
        }

        if (string.IsNullOrWhiteSpace(customer.LastName))
        {
            customer.LastName = NormalizeOptionalValue(identity.LastName, 50);
        }

        if (identity.EmailVerifiedUtc.HasValue)
        {
            customer.EmailVerifiedUtc ??= identity.EmailVerifiedUtc.Value;
        }
    }
}
