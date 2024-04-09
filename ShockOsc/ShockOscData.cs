using System.Collections.Concurrent;
using OpenShock.ShockOsc.Models;

namespace OpenShock.ShockOsc;

public sealed class ShockOscData
{
    public readonly ConcurrentDictionary<Guid, ProgramGroup> ProgramGroups = new();
}