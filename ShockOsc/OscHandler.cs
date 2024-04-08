using Microsoft.Extensions.Logging;

namespace OpenShock.ShockOsc;

public sealed class OscHandler
{
    
    
    public OscHandler(ILogger<OscHandler> logger)
    {
        logger.LogInformation("YES STARTED");
    }
}