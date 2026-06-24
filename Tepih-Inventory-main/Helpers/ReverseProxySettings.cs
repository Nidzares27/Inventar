namespace Inventar.Helpers;

public class ReverseProxySettings
{
    public bool Enabled { get; set; }

    public List<string> KnownProxies { get; set; } = [];

    public List<string> KnownNetworks { get; set; } = [];
}
