using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Formatting;
using System.Diagnostics;
using MudBlazor;

namespace Serilog;

public class LogStore
{
    public static List<LogEntry> Logs = new();

    public static void AddLog(string log)
    {
        if (Logs.Count == 0)
        {
            Logs.Add(new LogEntry { Time = DateTime.Now, Message = log });
            return;
        }
        // add to start of list
        Logs.Insert(0, new LogEntry { Time = DateTime.Now, Message = log });
    }

    public class LogEntry
    {
        public DateTime Time { get; set; }
        public string Message { get; set; }
    }
}

public class MySink : ILogEventSink
{
    private TextWriter _textWriter;
    private readonly ITextFormatter _formatProvider;
    public static Action<string, Severity>? NotificationAction { get; set; }

    public MySink(ITextFormatter formatProvider)
    {
        SinkExtensions.Instance = this;
        _formatProvider = formatProvider;
    }

    public void Emit(LogEvent logEvent)
    {
        _textWriter = new StringWriter();
        _formatProvider.Format(logEvent, _textWriter);
        // var logMessage = logEvent.RenderMessage(_formatProvider);
        var logMessage = _textWriter.ToString();
        if (logMessage == null) return;
        if (logMessage.StartsWith("[Microsoft.AspNetCore.Http.Connections.Client.Internal.LoggingHttpMessageHandler] "))
        {
            OpenShock.ShockOsc.ShockOsc.SetAuthLoading?.Invoke(false, false);
            NotificationAction?.Invoke(logMessage[82..], Severity.Error);
        }

        Debug.WriteLine(logMessage);
        LogStore.AddLog(logMessage);
        _textWriter.Flush();
    }
}

public static class SinkExtensions
{
    public static MySink? Instance;

    const string DefaultOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {Message}{NewLine}{Exception}";

    public static LoggerConfiguration MySink(
        this LoggerSinkConfiguration sinkConfiguration,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        string outputTemplate = DefaultOutputTemplate,
        IFormatProvider formatProvider = null,
        LoggingLevelSwitch levelSwitch = null)
    {
        if (outputTemplate == null) throw new ArgumentNullException(nameof(outputTemplate));

        var formatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
        var sink = new MySink(formatter);
        return sinkConfiguration.Sink(sink, restrictedToMinimumLevel, levelSwitch);
    }
}