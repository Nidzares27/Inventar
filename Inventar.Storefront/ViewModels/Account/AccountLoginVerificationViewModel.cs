using System.ComponentModel.DataAnnotations;

namespace Inventar.Storefront.ViewModels.Account;

public class AccountLoginVerificationViewModel
{
    [Required(ErrorMessage = "Kod je obavezan.")]
    [Display(Name = "Verifikacioni kod")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Kod mora imati 6 cifara.")]
    public string Code { get; set; } = string.Empty;

    public string MaskedEmail { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}
