using System.Collections;

namespace OpenShock.ShockOsc;

public static class PipeHelper
{
    public static IEnumerable<string> EnumeratePipes() {
        bool MoveNextSafe(IEnumerator enumerator) {

            // Pipes might have illegal characters in path. Seen one from IAR containing < and >.
            // The FileSystemEnumerable.MoveNext source code indicates that another call to MoveNext will return
            // the next entry.
            // Pose a limit in case the underlying implementation changes somehow. This also means that no more than 10
            // pipes with bad names may occur in sequence.
            const int retries = 10;
            for (int i = 0; i < retries; i++) {
                try {
                    return enumerator.MoveNext();
                } catch (ArgumentException) {
                }
            }
            Console.WriteLine("Pipe enumeration: Retry limit due to bad names reached.");
            return false;
        }

        using (var enumerator = Directory.EnumerateFiles(@"\\.\pipe\").GetEnumerator()) {
            while (MoveNextSafe(enumerator)) {
                yield return enumerator.Current;
            }
        }
    }
}