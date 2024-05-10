using System.Collections.Concurrent;
using OpenShock.ShockOsc.Models;

namespace OpenShock.ShockOsc.Services;

// In a perfect world, this class would not exist.
// But we kinda need it for now, dunno if it is possible to be removed ever.
public sealed class ShockOscData
{
    public ConcurrentDictionary<Guid, ProgramGroup> ProgramGroups { get; } = new();
    
    public bool IsMuted { get; set; }
}