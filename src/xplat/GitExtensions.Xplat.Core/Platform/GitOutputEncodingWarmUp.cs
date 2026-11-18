using System.Text;
using GitCommands;
using GitExtensions.Extensibility;

namespace GitExtensions.Xplat.Core.Platform;

/// <summary>
///  Works around a race in upstream's <c>GitCommands/Git/ExecutableExtensions.cs</c> (PLAN.md section 5, "Upstream defects
///  worked around in fork code"): its default output encoding is
///  <c>new Lazy&lt;Encoding&gt;(() =&gt; GitModule.SystemEncoding, false)</c>, a lazy that is not thread-safe. When the
///  first two git commands of a process run on two threads at once (the browse window reads the repository's refs and its
///  commits in parallel when it opens), both create the value and the second throws "ValueFactory attempted to access the
///  Value property of this instance", which the window shows as an error and the repository does not load. Creating the
///  value once on the UI thread at start-up, before any git command runs, removes the race; upstream's file is not changed.
/// </summary>
public static class GitOutputEncodingWarmUp
{
    /// <summary>
    ///  Creates upstream's default output encoding. Runs no process: the executable it passes throws as soon as it is
    ///  asked to start, after the encoding was read.
    /// </summary>
    public static void Run()
    {
        // GetOutputAsync reads the lazy synchronously, before its first await; Start then throws into the returned task.
        Task<string> task = new NoProcessExecutable().GetOutputAsync();
        if (task.Exception?.InnerException is not NotStartedException)
        {
            // Upstream's method changed so that it no longer starts the executable at once: say so, rather than leave the
            // race silently open again.
            throw new InvalidOperationException(
                "Upstream's ExecutableExtensions.GetOutputAsync changed; review GitOutputEncodingWarmUp.", task.Exception);
        }
    }

    private sealed class NotStartedException : Exception;

    private sealed class NoProcessExecutable : IExecutable
    {
        public string WorkingDir => "";

        public string Command => "";

        public string PrefixArguments => "";

        public IProcess Start(ArgumentString arguments = default, bool createWindow = false, bool redirectInput = false,
            bool redirectOutput = false, Encoding? outputEncoding = null, bool useShellExecute = false,
            bool throwOnErrorExit = true, CancellationToken cancellationToken = default)
            => throw new NotStartedException();
    }
}
