using CommandLine;

namespace OpenShock.ShockOsc.Cli;

public sealed class CliOptions
{
    [Option('h', "headless", Required = false, Default = false, HelpText = "Run the application in headless mode.")]
    public bool Headless { get; set; }
}