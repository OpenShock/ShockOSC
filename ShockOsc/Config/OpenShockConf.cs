namespace OpenShock.ShockOsc.Config;

public sealed class OpenShockConf
{
    public Uri Backend { get; set; } = new("https://api.openshock.app");
    public string Token { get; set; } = "";
    public IReadOnlyDictionary<Guid, ShockerConf> Shockers { get; set; } = new Dictionary<Guid, ShockerConf>();

    public sealed class ShockerConf
    {
        public bool Enabled { get; set; } = true;
    }
}