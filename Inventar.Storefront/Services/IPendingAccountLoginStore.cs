namespace Inventar.Storefront.Services;

public interface IPendingAccountLoginStore
{
    PendingAccountLoginSessionModel? Get();
    void Save(PendingAccountLoginSessionModel model);
    void Clear();
}
