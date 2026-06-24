using Inventar.Storefront.Models;

namespace Inventar.Storefront.Services;

public interface ICartService
{
    IReadOnlyList<CartItem> GetCart();
    void Store(IReadOnlyCollection<CartItem> cartItems);
    void Remove(string lineId);
    void Clear();
    int GetTotalItemCount();
}
