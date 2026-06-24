using System.Security.Claims;
using Inventar.Storefront.Models;

namespace Inventar.Storefront.Services;

public interface IStorefrontCustomerService
{
    Task<StorefrontCustomer?> GetCurrentCustomerAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<IssuedLoginCodeResult> IssueLoginCodeAsync(string email, bool rememberMe, CancellationToken cancellationToken = default);
    Task<VerifiedLoginCodeResult> VerifyLoginCodeAsync(string email, string code, CancellationToken cancellationToken = default);
    Task<StorefrontCustomer> GetOrCreateByVerifiedEmailAsync(
        string email,
        StorefrontVerifiedIdentityData? identity = null,
        CancellationToken cancellationToken = default);
    Task LinkOrdersByEmailAsync(StorefrontCustomer customer, CancellationToken cancellationToken = default);
    Task SaveProfileAsync(StorefrontCustomer customer, StorefrontCustomerProfileData profile, CancellationToken cancellationToken = default);
    ClaimsPrincipal CreatePrincipal(StorefrontCustomer customer);
}

public sealed class IssuedLoginCodeResult
{
    public required string Email { get; init; }
    public required string VerificationCode { get; init; }
    public required DateTime ExpiresUtc { get; init; }
}

public sealed class VerifiedLoginCodeResult
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool RememberMe { get; init; }
    public StorefrontCustomer? Customer { get; init; }
}

public sealed class StorefrontCustomerProfileData
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Phone { get; init; }
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? City { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
}

public sealed class StorefrontVerifiedIdentityData
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public DateTime? EmailVerifiedUtc { get; init; }
}
