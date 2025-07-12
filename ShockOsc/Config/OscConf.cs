using System.Net;

namespace OpenShock.ShockOSC.Config;

public sealed class OscConf
{
    public bool Hoscy { get; set; } = false;
    public ushort HoscySendPort { get; set; } = 9001;
    public bool QuestSupport { get; set; } = false;
    public bool OscQuery { get; set; } = true;
    public ushort OscSendPort { get; set; } = 9000;
    public ushort OscReceivePort { get; set; } = 9001;
    
    public string OscSendIp { get; set; } = IPAddress.Loopback.ToString();
}