using Inventar.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Inventar.Utils
{
    public class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser>
    {
        public AppUserClaimsPrincipalFactory(
            UserManager<AppUser> userManager,
            IOptions<IdentityOptions> optionsAccessor)
            : base(userManager, optionsAccessor) { }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            identity.AddClaim(new Claim(ClaimTypes.GivenName, user.FirstName ?? ""));
            identity.AddClaim(new Claim(ClaimTypes.Surname, user.LastName ?? ""));

            return identity;
        }
    }
}
