using Inventar.Storefront.ViewModels.Account;
using Inventar.Storefront.ViewModels.Checkout;

namespace Inventar.Storefront.Services;

public interface IStorefrontEmailService
{
    Task SendAccountLoginCodeAsync(
        AccountLoginEmailViewModel model,
        CancellationToken cancellationToken = default);

    Task SendCheckoutVerificationCodeAsync(
        CheckoutVerificationEmailModel model,
        CancellationToken cancellationToken = default);

    Task SendOrderConfirmationAsync(
        OrderConfirmationViewModel model,
        CancellationToken cancellationToken = default);
}
