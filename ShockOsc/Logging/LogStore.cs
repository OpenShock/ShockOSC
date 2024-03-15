using System.Collections.Concurrent;
using Serilog.Events;

namespace OpenShock.ShockOsc.Logging;

public static class LogStore
{
    public static readonly ConcurrentQueue<LogEntry> Logs = new();
    public static Action? OnLogAdded;

    public static void AddLog(LogEntry log)
    {
        Logs.Enqueue(log);
        if(Logs.Count > 1000) Logs.TryDequeue(out _);
        
        OnLogAdded?.Invoke();
    }

    public class LogEntry
    {
        public required LogEventLevel Level { get; init; }
        public required DateTimeOffset Time { get; init; }
        public required string Message { get; init; }
        public required string SourceContext { get; init; }
        public required string SourceContextShort { get; init; }
        
        // UI Data

        public bool IsExpanded { get; set; } = false;
    }
}