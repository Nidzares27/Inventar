using System.ComponentModel.DataAnnotations;

namespace Inventar.Storefront.ViewModels.Checkout;

public class CheckoutVerificationViewModel
{
    [Required(ErrorMessage = "Unesite verifikacioni kod.")]
    [Display(Name = "Verifikacioni kod")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Kod mora imati 6 cifara.")]
    public string Code { get; set; } = string.Empty;

    public string MaskedEmail { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; }
}
