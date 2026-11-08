using System.Diagnostics;

namespace GitExtensions.Xplat.Core.Platform;

/// <summary>
///  A program started from Finder or the Dock on macOS gets a minimal <c>PATH</c> (<c>/usr/bin:/bin:/usr/sbin:/sbin</c>), so
///  git-lfs, gpg, difftools, hooks and credential helpers that git and the app start are not found. The app reads the
///  <c>PATH</c> of the user's login shell once at startup and gives it to everything it starts.
/// </summary>
public static class LoginShellPath
{
    private const string PathVariable = "PATH";
    private const string ShellVariable = "SHELL";
    private const string TerminalVariable = "TERM";
    private const string DefaultMacOSShell = "/bin/zsh";

    // An absolute program, not the shell's own echo: every shell then prints the exported form, with colons, where fish
    // would print its list with spaces.
    private const string PrintPathCommand = "/usr/bin/printenv PATH";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(3);

    /// <summary>
    ///  Whether the PATH needs reading: on macOS, unless a terminal started the app and so gave it the user's PATH already.
    /// </summary>
    public static bool ShouldRead(bool isMacOS, string? terminal) => isMacOS && string.IsNullOrEmpty(terminal);

    /// <summary>
    ///  Runs <paramref name="shell"/> as a login shell and returns its PATH, or null when it fails or takes longer than
    ///  <paramref name="timeout"/>: a slow or broken shell configuration must not stop the app.
    /// </summary>
    public static string? Read(string shell, TimeSpan timeout)
    {
        try
        {
            ProcessStartInfo info = new(shell)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };
            info.ArgumentList.Add("-l");
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add(PrintPathCommand);

            using Process process = new() { StartInfo = info };
            using ManualResetEventSlim outputEnded = new();
            List<string> lines = [];
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    outputEnded.Set();
                }
                else
                {
                    lock (lines)
                    {
                        lines.Add(e.Data);
                    }
                }
            };

            process.Start();
            process.StandardInput.Close();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Stopwatch elapsed = Stopwatch.StartNew();
            if (!process.WaitForExit(timeout) || !outputEnded.Wait(timeout - elapsed.Elapsed))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            lock (lines)
            {
                return process.ExitCode == 0 ? LastLine(lines) : null;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentOutOfRangeException)
        {
            Trace.WriteLine($"Could not read the login shell's PATH: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///  The login shell's folders first, as in the user's terminal, then the folders only the process had.
    /// </summary>
    public static string Merge(string? login, string? current)
        => string.Join(Path.PathSeparator, Folders(login).Concat(Folders(current)).Distinct(StringComparer.Ordinal));

    /// <summary>
    ///  At startup: gives this process, and so everything it starts, the user's PATH when it needs one.
    /// </summary>
    public static void Apply()
    {
        if (!ShouldRead(OperatingSystem.IsMacOS(), Environment.GetEnvironmentVariable(TerminalVariable)))
        {
            return;
        }

        string shell = Environment.GetEnvironmentVariable(ShellVariable) is { Length: > 0 } configured ? configured : DefaultMacOSShell;
        if (Read(shell, _timeout) is { } login)
        {
            Environment.SetEnvironmentVariable(PathVariable, Merge(login, Environment.GetEnvironmentVariable(PathVariable)));
        }
    }

    private static IEnumerable<string> Folders(string? path)
        => (path ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

    // A shell's configuration may print a greeting first; the command's output comes last.
    private static string? LastLine(IEnumerable<string> lines) => lines.LastOrDefault(line => line.Length > 0);
}
