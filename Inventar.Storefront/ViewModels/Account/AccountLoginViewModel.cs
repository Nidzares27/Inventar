using System.ComponentModel.DataAnnotations;

namespace Inventar.Storefront.ViewModels.Account;

public class AccountLoginViewModel
{
    [Required(ErrorMessage = "Email je obavezan.")]
    [EmailAddress(ErrorMessage = "Unesite ispravnu email adresu.")]
    [Display(Name = "Email")]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Zapamti me na ovom uredjaju")]
    public bool RememberMe { get; set; } = true;

    public string? ReturnUrl { get; set; }

    public bool IsGoogleLoginAvailable { get; set; }
}
