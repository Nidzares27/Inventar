using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels.Login_Register
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
