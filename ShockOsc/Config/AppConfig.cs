using Semver;

namespace OpenShock.ShockOsc.Config;

public sealed class AppConfig
{
    public bool CloseToTray { get; set; } = true;
    public bool DiscordPreview { get; set; } = false;
    
    public UpdateChannel UpdateChannel { get; set; } = UpdateChannel.Release;
    public SemVersion? LastIgnoredVersion { get; set; } = null;
}

public enum UpdateChannel
{
    Release,
    PreRelease
}