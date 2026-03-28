using System.ComponentModel.DataAnnotations;

namespace Inventar.Storefront.ViewModels.Checkout;

public class CheckoutFormViewModel
{
    [Required(ErrorMessage = "Ime je obavezno.")]
    [Display(Name = "Ime")]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Prezime je obavezno.")]
    [Display(Name = "Prezime")]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Unesite ispravnu email adresu.")]
    [Display(Name = "Email (opciono)")]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefon je obavezan.")]
    [Display(Name = "Telefon")]
    [StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Adresa je obavezna.")]
    [Display(Name = "Adresa")]
    [StringLength(200)]
    public string AddressLine1 { get; set; } = string.Empty;

    [Display(Name = "Dodatna adresa")]
    [StringLength(200)]
    public string? AddressLine2 { get; set; }

    [Required(ErrorMessage = "Grad je obavezan.")]
    [Display(Name = "Grad")]
    [StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Display(Name = "Poštanski broj")]
    [StringLength(20)]
    public string? PostalCode { get; set; }

    [Required(ErrorMessage = "Država je obavezna.")]
    [Display(Name = "Država")]
    [StringLength(100)]
    public string Country { get; set; } = "Crna Gora";

    [Display(Name = "Napomena uz narudžbu")]
    public string? CustomerNote { get; set; }
}
