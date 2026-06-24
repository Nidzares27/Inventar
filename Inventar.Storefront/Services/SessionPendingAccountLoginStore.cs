using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Inventar.Storefront.Services;

public class SessionPendingAccountLoginStore : IPendingAccountLoginStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SessionPendingAccountLoginStore> _logger;

    public SessionPendingAccountLoginStore(
        IHttpContextAccessor httpContextAccessor,
        ILogger<SessionPendingAccountLoginStore> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public PendingAccountLoginSessionModel? Get()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        var json = session?.GetString(StorefrontAuthenticationConstants.PendingLoginSessionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PendingAccountLoginSessionModel>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Ignoring invalid pending account login session payload and clearing session state.");
            Clear();
            return null;
        }
    }

    public void Save(PendingAccountLoginSessionModel model)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session is null)
        {
            return;
        }

        session.SetString(
            StorefrontAuthenticationConstants.PendingLoginSessionKey,
            JsonSerializer.Serialize(model, SerializerOptions));
    }

    public void Clear()
    {
        _httpContextAccessor.HttpContext?.Session.Remove(StorefrontAuthenticationConstants.PendingLoginSessionKey);
    }
}
