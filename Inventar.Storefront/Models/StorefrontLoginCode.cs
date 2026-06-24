using System.ComponentModel.DataAnnotations;

namespace Inventar.Storefront.Models;

public class StorefrontLoginCode
{
    public int Id { get; set; }

    [Required]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(254)]
    public string NormalizedEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Purpose { get; set; } = StorefrontLoginCodePurposes.AccountLogin;

    [Required]
    [StringLength(128)]
    public string CodeHash { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
    public int FailedAttemptCount { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime? UsedUtc { get; set; }
}
