using Serilog.Core;
using Serilog.Events;

namespace Inventar.Utils
{
    public class UserEnricher : ILogEventEnricher
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserEnricher(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var context = _httpContextAccessor.HttpContext;
            string userName = "Anonymous";

            if (context?.User?.Identity?.IsAuthenticated == true)
            {
                userName = context.User.Identity.Name ?? "Unnamed";
            }

            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserName", userName));
        }
    }
}
