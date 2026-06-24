using System.Text.Json;
using Inventar.Storefront.Models;
using Microsoft.Extensions.Logging;

namespace Inventar.Storefront.Services;

public class SessionCartService : ICartService
{
    private const string SessionKey = "storefront-cart";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SessionCartService> _logger;

    public SessionCartService(
        IHttpContextAccessor httpContextAccessor,
        ILogger<SessionCartService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public IReadOnlyList<CartItem> GetCart()
    {
        var session = GetSession();
        var payload = session.GetString(SessionKey);

        if (string.IsNullOrWhiteSpace(payload))
        {
            return Array.Empty<CartItem>();
        }

        List<CartItem> cart;
        try
        {
            cart = JsonSerializer.Deserialize<List<CartItem>>(payload) ?? new List<CartItem>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Ignoring invalid storefront cart session payload and clearing the cart session.");
            session.Remove(SessionKey);
            return Array.Empty<CartItem>();
        }

        var normalizedCart = cart
            .Where(line => line.ProductId > 0 && !string.IsNullOrWhiteSpace(line.LineId))
            .Select(line => new CartItem
            {
                LineId = string.IsNullOrWhiteSpace(line.LineId) ? Guid.NewGuid().ToString("N") : line.LineId,
                ProductId = line.ProductId,
                Quantity = Math.Max(line.Quantity, 1),
                PoMjeri = line.PoMjeri,
                CustomWidth = line.CustomWidth,
                CustomLength = line.CustomLength,
                SelectedColor = string.IsNullOrWhiteSpace(line.SelectedColor) ? null : line.SelectedColor.Trim(),
                Allocations = line.Allocations?
                    .Where(allocation => allocation.SourceProductId > 0 && allocation.Quantity > 0 && allocation.ConsumedLengthPerUnit > 0)
                    .Select(allocation => new CartItemAllocation
                    {
                        SourceProductId = allocation.SourceProductId,
                        Quantity = Math.Max(allocation.Quantity, 1),
                        ConsumedLengthPerUnit = Math.Max(allocation.ConsumedLengthPerUnit, 1)
                    })
                    .ToList() ?? new List<CartItemAllocation>()
            })
            .ToList();

        if (normalizedCart.Count != cart.Count || CartChanged(cart, normalizedCart))
        {
            Store(normalizedCart);
        }

        return normalizedCart;
    }

    public void Store(IReadOnlyCollection<CartItem> cartItems)
    {
        var normalizedCart = cartItems
            .Where(item => item.ProductId > 0 && !string.IsNullOrWhiteSpace(item.LineId))
            .Select(item => new CartItem
            {
                LineId = item.LineId,
                ProductId = item.ProductId,
                Quantity = Math.Max(item.Quantity, 1),
                PoMjeri = item.PoMjeri,
                CustomWidth = item.CustomWidth,
                CustomLength = item.CustomLength,
                SelectedColor = string.IsNullOrWhiteSpace(item.SelectedColor) ? null : item.SelectedColor.Trim(),
                Allocations = item.Allocations
                    .Where(allocation => allocation.SourceProductId > 0 && allocation.Quantity > 0 && allocation.ConsumedLengthPerUnit > 0)
                    .Select(allocation => new CartItemAllocation
                    {
                        SourceProductId = allocation.SourceProductId,
                        Quantity = Math.Max(allocation.Quantity, 1),
                        ConsumedLengthPerUnit = Math.Max(allocation.ConsumedLengthPerUnit, 1)
                    })
                    .ToList()
            })
            .ToList();

        var session = GetSession();
        session.SetString(SessionKey, JsonSerializer.Serialize(normalizedCart));
    }

    public void Remove(string lineId)
    {
        var cart = GetCart().ToList();
        var existingLine = cart.FirstOrDefault(line => string.Equals(line.LineId, lineId, StringComparison.Ordinal));
        if (existingLine == null)
        {
            return;
        }

        cart.Remove(existingLine);
        Store(cart);
    }

    public void Clear()
    {
        GetSession().Remove(SessionKey);
    }

    public int GetTotalItemCount()
    {
        return GetCart().Sum(line => line.Quantity);
    }

    private static bool CartChanged(IReadOnlyList<CartItem> original, IReadOnlyList<CartItem> normalized)
    {
        if (original.Count != normalized.Count)
        {
            return true;
        }

        for (var index = 0; index < original.Count; index++)
        {
            var left = original[index];
            var right = normalized[index];
            if (left.LineId != right.LineId ||
                left.ProductId != right.ProductId ||
                left.Quantity != right.Quantity ||
                left.PoMjeri != right.PoMjeri ||
                left.CustomWidth != right.CustomWidth ||
                left.CustomLength != right.CustomLength ||
                !string.Equals(left.SelectedColor, right.SelectedColor, StringComparison.OrdinalIgnoreCase) ||
                left.Allocations.Count != right.Allocations.Count)
            {
                return true;
            }

            for (var allocationIndex = 0; allocationIndex < left.Allocations.Count; allocationIndex++)
            {
                var leftAllocation = left.Allocations[allocationIndex];
                var rightAllocation = right.Allocations[allocationIndex];

                if (leftAllocation.SourceProductId != rightAllocation.SourceProductId ||
                    leftAllocation.Quantity != rightAllocation.Quantity ||
                    leftAllocation.ConsumedLengthPerUnit != rightAllocation.ConsumedLengthPerUnit)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private ISession GetSession()
    {
        return _httpContextAccessor.HttpContext?.Session
            ?? throw new InvalidOperationException("Session is not available for the current request.");
    }
}
