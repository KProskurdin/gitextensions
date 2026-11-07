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
///  <c>x-terminal-emulator</c> convention, or the <c>TERMINAL</c> environment variable's program).
/// </summary>
public static class InteractivePatch
{
    /// <summary>
    ///  The environment variable that names the terminal emulator, as the toolbar's terminal button reads it.
    /// </summary>
    public const string TerminalEnvironmentVariable = "TERMINAL";

    // The terminal emulator's option before the command it runs.
    private const string ExecuteOption = "-e";

    /// <summary>
    ///  Whether the items can run here: macOS' Terminal opens no command it can be given and waited for.
    /// </summary>
    public static bool IsAvailable(HostPlatform platform) => !platform.IsMacOS;

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

            (string fileName, string arguments) = TerminalCommand(
                Environment.GetEnvironmentVariable(TerminalEnvironmentVariable),
                module.GitExecutable.Command,
                GitArguments(path, reset));
            using IProcess process = new Executable(fileName, workingDir).Start(arguments, throwOnErrorExit: false);
            return await process.WaitForExitAsync() == 0;
        });
}
