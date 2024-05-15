using CommandLine;

namespace OpenShock.ShockOsc.Cli;

public static class ParseHelper
{
    public static void Parse<T>(string[] args, Action<T> success)
    {
        var parsed = Parser.Default.ParseArguments<T>(args);
        parsed.WithParsed(success);
        parsed.WithNotParsed(errors =>
        {
            errors.Output();
            Environment.Exit(1);
        });
    }
    
    public static async Task ParseAsync<T>(string[] args, Func<T, Task> success)
    {
        var parsed = Parser.Default.ParseArguments<T>(args);
        await parsed.WithParsedAsync(success);
        parsed.WithNotParsed(errors =>
        {
            errors.Output();
            Environment.Exit(1);
        });
    }
}