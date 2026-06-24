using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Inventar.Storefront.Services;

public class SessionPendingCheckoutStore : IPendingCheckoutStore
{
    private const string SessionKey = "storefront-pending-checkout";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SessionPendingCheckoutStore> _logger;

    public SessionPendingCheckoutStore(
        IHttpContextAccessor httpContextAccessor,
        ILogger<SessionPendingCheckoutStore> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public PendingCheckoutSessionModel? Get()
    {
        var payload = GetSession().GetString(SessionKey);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PendingCheckoutSessionModel>(payload);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Ignoring invalid pending checkout session payload and clearing session state.");
            Clear();
            return null;
        }
    }

    public void Save(PendingCheckoutSessionModel model)
    {
        GetSession().SetString(SessionKey, JsonSerializer.Serialize(model));
    }

    public void Clear()
    {
        GetSession().Remove(SessionKey);
    }

    private ISession GetSession()
    {
        return _httpContextAccessor.HttpContext?.Session
            ?? throw new InvalidOperationException("Session is not available for the current request.");
    }
}
