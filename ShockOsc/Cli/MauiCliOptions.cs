using CommandLine;

namespace OpenShock.ShockOsc.Cli;

public sealed class MauiCliOptions : CliOptions
{
    [Option('c', "console", Required = false, Default = false, HelpText = "Create console window for stdout/stderr.")]
    public required bool Console { get; init; }
    
    [Option("uri", Required = false, HelpText = "Custom URI for callbacks")]
    public required string Uri { get; init; }
}