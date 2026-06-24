using Inventar.Storefront.Models;

namespace Inventar.Storefront.Services;

public interface ICheckoutService
{
    Task<CheckoutResult> CreateCashOnDeliveryOrderAsync(
        CheckoutRequest request,
        IReadOnlyCollection<CartItem> cartItems,
        int? storefrontCustomerId = null,
        CancellationToken cancellationToken = default);
}

public sealed class CheckoutRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string AddressLine1 { get; init; } = string.Empty;
    public string? AddressLine2 { get; init; }
    public string City { get; init; } = string.Empty;
    public string? PostalCode { get; init; }
    public string Country { get; init; } = string.Empty;
    public string? CustomerNote { get; init; }
}

public sealed class CheckoutResult
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? OrderNumber { get; init; }
}
