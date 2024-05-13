using CommandLine;

namespace OpenShock.ShockOsc.Cli;

public static class ParseHelper
{
    public static void Parse(string[] args, Action<CliOptions> success)
    {
        var parsed = Parser.Default.ParseArguments<CliOptions>(args);
        parsed.WithParsed(success);
        parsed.WithNotParsed(errors =>
        {
            errors.Output();
            Environment.Exit(1);
        });
    }
    
    public static async Task ParseAsync(string[] args, Func<CliOptions, Task> success)
    {
        var parsed = Parser.Default.ParseArguments<CliOptions>(args);
        await parsed.WithParsedAsync(success);
        parsed.WithNotParsed(errors =>
        {
            errors.Output();
            Environment.Exit(1);
        });
    }
}