using System.Text.Json;
using Inventar.Storefront.Models;

namespace Inventar.Storefront.Services;

public class SessionCartService : ICartService
{
    private const string SessionKey = "storefront-cart";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public SessionCartService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyList<CartItem> GetCart()
    {
        var session = GetSession();
        var payload = session.GetString(SessionKey);

        if (string.IsNullOrWhiteSpace(payload))
        {
            return Array.Empty<CartItem>();
        }

        return JsonSerializer.Deserialize<List<CartItem>>(payload) ?? new List<CartItem>();
    }

    public void AddOrIncrement(int productId, int quantity, int maxAvailableQuantity)
    {
        var cart = GetCart().ToList();
        var existingLine = cart.FirstOrDefault(line => line.ProductId == productId);

        if (existingLine == null)
        {
            cart.Add(new CartItem
            {
                ProductId = productId,
                Quantity = ClampQuantity(quantity, maxAvailableQuantity)
            });
        }
        else
        {
            existingLine.Quantity = ClampQuantity(existingLine.Quantity + quantity, maxAvailableQuantity);
        }

        SaveCart(cart);
    }

    public void SetQuantity(int productId, int quantity, int maxAvailableQuantity)
    {
        var cart = GetCart().ToList();
        var existingLine = cart.FirstOrDefault(line => line.ProductId == productId);
        if (existingLine == null)
        {
            return;
        }

        var normalizedQuantity = ClampQuantity(quantity, maxAvailableQuantity);
        if (normalizedQuantity <= 0)
        {
            cart.Remove(existingLine);
        }
        else
        {
            existingLine.Quantity = normalizedQuantity;
        }

        SaveCart(cart);
    }

    public void Remove(int productId)
    {
        var cart = GetCart().ToList();
        var existingLine = cart.FirstOrDefault(line => line.ProductId == productId);
        if (existingLine == null)
        {
            return;
        }

        cart.Remove(existingLine);
        SaveCart(cart);
    }

    public void Clear()
    {
        GetSession().Remove(SessionKey);
    }

    public int GetTotalItemCount()
    {
        return GetCart().Sum(line => line.Quantity);
    }

    private static int ClampQuantity(int quantity, int maxAvailableQuantity)
    {
        if (maxAvailableQuantity <= 0)
        {
            return 0;
        }

        return Math.Min(Math.Max(quantity, 1), maxAvailableQuantity);
    }

    private void SaveCart(IReadOnlyCollection<CartItem> cart)
    {
        var session = GetSession();
        session.SetString(SessionKey, JsonSerializer.Serialize(cart));
    }

    private ISession GetSession()
    {
        return _httpContextAccessor.HttpContext?.Session
            ?? throw new InvalidOperationException("Session is not available for the current request.");
    }
}
