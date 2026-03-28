using Inventar.Storefront.Models;

namespace Inventar.Storefront.Services;

public interface ICartService
{
    IReadOnlyList<CartItem> GetCart();
    void AddOrIncrement(int productId, int quantity, int maxAvailableQuantity);
    void SetQuantity(int productId, int quantity, int maxAvailableQuantity);
    void Remove(int productId);
    void Clear();
    int GetTotalItemCount();
}
