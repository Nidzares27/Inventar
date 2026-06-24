namespace Inventar.Helpers;

public class HostRedirectSettings
{
    public const string SectionName = "HostRedirect";

    public bool Enabled { get; set; }

    public bool Permanent { get; set; }

    public string DestinationHost { get; set; } = string.Empty;

    public List<string> SourceHosts { get; set; } = [];

    public bool HasValidConfiguration()
    {
        return !string.IsNullOrWhiteSpace(DestinationHost) && SourceHosts.Count > 0;
    }

    public bool ShouldRedirect(string? requestHost)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(requestHost) || string.IsNullOrWhiteSpace(DestinationHost))
        {
            return false;
        }

        if (string.Equals(requestHost, DestinationHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return SourceHosts.Any(sourceHost => string.Equals(sourceHost, requestHost, StringComparison.OrdinalIgnoreCase));
    }
}
