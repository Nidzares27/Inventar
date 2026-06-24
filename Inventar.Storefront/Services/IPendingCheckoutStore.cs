namespace Inventar.Storefront.Services;

public interface IPendingCheckoutStore
{
    PendingCheckoutSessionModel? Get();
    void Save(PendingCheckoutSessionModel model);
    void Clear();
}
