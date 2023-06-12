using System.Runtime.CompilerServices;
using Serilog;

namespace ShockLink.ShockOsc.Utils;

public static class SlTask
{

    public static Task Run(Func<Task?> function, CancellationToken token = default, [CallerFilePath] string file = "",
        [CallerMemberName] string member = "", [CallerLineNumber] int line = -1) => Task.Run(function, token).ContinueWith(
        t =>
        {
            if (!t.IsFaulted) return;
            var index = file.LastIndexOf('\\');
            if (index == -1) index = file.LastIndexOf('/');
            Log.Error(t.Exception,
                "Error during task execution. {File}::{Member}:{Line}",
                file.Substring(index + 1, file.Length - index - 1), member, line);
        }, TaskContinuationOptions.OnlyOnFaulted);
}