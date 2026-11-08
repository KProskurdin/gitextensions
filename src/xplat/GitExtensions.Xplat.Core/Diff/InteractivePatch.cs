using System.Diagnostics;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Repository;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  Upstream FileStatusList's "Interactive add..." and "Reset chunk of file..." (<c>InteractiveAdd_Click</c> and
///  <c>ResetChunkOfFile_Click</c>): git's interactive <c>add --patch</c> or <c>checkout -p</c> of one working directory
///  file in a window of its own, waited for. On Windows that is upstream's own <c>GitModule.AddInteractiveAsync</c> and
///  <c>ResetInteractiveAsync</c> (git in a console window). Elsewhere a program started from the desktop has no terminal,
///  so git runs in the system's terminal emulator, given git's command after <c>-e</c> (the Debian
///  <c>x-terminal-emulator</c> convention, or the <c>TERMINAL</c> environment variable's program). macOS' Terminal takes no
///  command and cannot be waited for per window, so it opens a script that leaves git's exit code in a file.
/// </summary>
public static class InteractivePatch
{
    /// <summary>
    ///  The environment variable that names the terminal emulator, as the toolbar's terminal button reads it.
    /// </summary>
    public const string TerminalEnvironmentVariable = "TERMINAL";

    // The terminal emulator's option before the command it runs.
    private const string ExecuteOption = "-e";

    private const string MacScriptFolderPrefix = "gitextensions-patch-";

    // Terminal runs a file with this extension when asked to open it.
    private const string MacScriptFileName = "patch.command";
    private const string ExitCodeFileName = "exit-code";
    private const string ShellInterruptedExitCode = "1";

    private static readonly TimeSpan _exitCodePollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    ///  Upstream's git arguments: <c>add --patch</c> or, to reset, <c>checkout -p</c> with the file's quoted path.
    /// </summary>
    public static ArgumentString GitArguments(string path, bool reset)
        => reset
            ? new GitArgumentBuilder("checkout") { "-p", path.Quote() }
            : new GitArgumentBuilder("add") { "--patch", path.Quote() };

    /// <summary>
    ///  The terminal emulator and its arguments that run <paramref name="gitCommand"/> with
    ///  <paramref name="gitArguments"/>, outside Windows.
    /// </summary>
    public static (string FileName, string Arguments) TerminalCommand(string? terminalEnvironmentValue,
        string gitCommand,
        ArgumentString gitArguments)
        => (Platform.TerminalCommand
                .Default(new HostPlatform(IsWindows: false, IsMacOS: false), terminalEnvironmentValue)
                .FileName, $"{ExecuteOption} {gitCommand.Quote()} {gitArguments}");

    /// <summary>
    ///  The script macOS' Terminal runs. The path is quoted for the shell, as a double-quoted name would expand <c>$</c>.
    ///  Closing the window hangs up the shell, which still has to leave the exit code, or the app would wait forever.
    /// </summary>
    public static string MacScript(string workingDir, string gitCommand, string path, bool reset, string exitCodeFile)
        => $$"""
            #!/bin/sh
            trap 'echo {{ShellInterruptedExitCode}} > {{ShellQuote(exitCodeFile)}}; exit {{ShellInterruptedExitCode}}' HUP INT TERM
            cd {{ShellQuote(workingDir)}} || { echo {{ShellInterruptedExitCode}} > {{ShellQuote(exitCodeFile)}}; exit {{ShellInterruptedExitCode}}; }
            {{ShellQuote(gitCommand)}} {{(reset ? "checkout -p" : "add --patch")}} {{ShellQuote(path)}}
            code=$?
            echo "$code" > {{ShellQuote(exitCodeFile)}}
            exit "$code"

            """;

    /// <summary>
    ///  Runs git's interactive patch of <paramref name="path"/> in <paramref name="workingDir"/> and waits until its window
    ///  closes; true when git succeeded (outside Windows, when the terminal did).
    /// </summary>
    public static Task<bool> RunAsync(string workingDir, string path, bool reset, HostPlatform platform)
        => Task.Run(async () =>
        {
            GitModule module = GitModules.Open(workingDir);
            if (platform.IsWindows)
            {
                GitItemStatus file = new(path);
                return reset ? await module.ResetInteractiveAsync(file) : await module.AddInteractiveAsync(file);
            }

            if (platform.IsMacOS)
            {
                return await RunInMacTerminalAsync(workingDir, module.GitExecutable.Command, path, reset);
            }

            (string fileName, string arguments) = TerminalCommand(
                Environment.GetEnvironmentVariable(TerminalEnvironmentVariable),
                module.GitExecutable.Command,
                GitArguments(path, reset));
            using IProcess process = new Executable(fileName, workingDir).Start(arguments, throwOnErrorExit: false);
            return await process.WaitForExitAsync() == 0;
        });

    private static async Task<bool> RunInMacTerminalAsync(string workingDir, string gitCommand, string path, bool reset)
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory(MacScriptFolderPrefix);
        try
        {
            string script = Path.Combine(folder.FullName, MacScriptFileName);
            string exitCodeFile = Path.Combine(folder.FullName, ExitCodeFileName);
            await File.WriteAllTextAsync(script, MacScript(workingDir, gitCommand, path, reset, exitCodeFile));
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            using Process? terminal = Process.Start(new ProcessStartInfo("open", ["-a", "Terminal", script]) { UseShellExecute = false });
            if (terminal is null)
            {
                return false;
            }

            await terminal.WaitForExitAsync();
            if (terminal.ExitCode != 0)
            {
                return false;
            }

            return await WaitForExitCodeAsync(exitCodeFile) == "0";
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    // The shell writes the file before it is done writing it only for an instant, so an empty read is read again.
    private static async Task<string> WaitForExitCodeAsync(string exitCodeFile)
    {
        while (true)
        {
            if (File.Exists(exitCodeFile) && (await File.ReadAllTextAsync(exitCodeFile)).Trim() is { Length: > 0 } code)
            {
                return code;
            }

            await Task.Delay(_exitCodePollInterval);
        }
    }

    private static string ShellQuote(string text) => $"'{text.Replace("'", @"'\''")}'";
}
