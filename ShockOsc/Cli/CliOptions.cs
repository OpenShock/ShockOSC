using CommandLine;

namespace OpenShock.ShockOsc.Cli;

public sealed class CliOptions
{
    [Option("headless", Required = false, Default = false, HelpText = "Run the application in headless mode.")]
    public required bool Headless { get; init; }
    
    [Option('c', "console", Required = false, Default = false, HelpText = "Create console window for stdout/stderr.")]
    public required bool Console { get; init; }
    
    [Option("uri", Required = false, HelpText = "Custom URI for callbacks")]
    public required string Uri { get; init; }
}