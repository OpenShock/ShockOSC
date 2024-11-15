using System.Reflection;
using Semver;

namespace OpenShock.ShockOsc;

public static class Constants
{
    public static readonly SemVersion Version = SemVersion.Parse(typeof(Constants).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, SemVersionStyles.Strict);
    public static readonly SemVersion VersionWithoutMetadata = Version.WithoutMetadata();
}