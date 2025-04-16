using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace Inventar.Utils
{
    public class SessionRequestCultureProvider : IRequestCultureProvider
    {
        private const string CultureSessionKey = "culture";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public SessionRequestCultureProvider()
        {
        }

        public SessionRequestCultureProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Task<ProviderCultureResult> DetermineProviderCultureResult(HttpContext httpContext)
        {
            // Try to get culture from the session
            var culture = httpContext.Session.GetString(CultureSessionKey);
            if (string.IsNullOrEmpty(culture))
            {
                // If no culture is found, use the default
                culture = "en-US";
            }

            var cultureInfo = new CultureInfo(culture);
            var uiCultureInfo = new CultureInfo(culture); // You can customize UI culture if needed

            return Task.FromResult(new ProviderCultureResult(cultureInfo.Name, uiCultureInfo.Name));
        }
    }
}
