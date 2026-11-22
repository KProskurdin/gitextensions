using System.Diagnostics;

namespace GitExtensions.Xplat.Core.Platform;

/// <summary>
///  The operating system the core is running on. Passed in so that per-OS behavior can be tested on any host.
/// </summary>
public sealed record HostPlatform(bool IsWindows, bool IsMacOS)
{
    public static HostPlatform Current => new(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS());
}

/// <summary>
///  Starts an external program without waiting for it and without going through a shell.
/// </summary>
public interface IProcessLauncher
{
    void Start(string fileName, IReadOnlyList<string> arguments, string workingDirectory);
}

public sealed class SystemProcessLauncher : IProcessLauncher
{
    public void Start(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        ProcessStartInfo info = new(fileName)
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory
        };
        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using Process? process = Process.Start(info);
        if (process is null)
        {
            throw new InvalidOperationException($"Could not start {fileName}");
        }
    }
}

public interface IFileManager
{
    /// <summary>
    ///  Shows <paramref name="folder"/> in the platform's file manager.
    /// </summary>
    void OpenFolder(string folder);
}

public sealed class SystemFileManager(IProcessLauncher launcher, HostPlatform platform) : IFileManager
{
    public void OpenFolder(string folder)
    {
        string fileName = platform.IsWindows ? "explorer.exe" : platform.IsMacOS ? "open" : "xdg-open";
        launcher.Start(fileName, [folder], folder);
    }
}

/// <summary>
///  A program and its arguments. The token <c>{folder}</c> in an argument is replaced by the folder to open.
/// </summary>
public sealed record TerminalCommand(string FileName, IReadOnlyList<string> Arguments)
{
    public const string FolderToken = "{folder}";

    /// <summary>
    ///  The command as <see cref="Parse"/> reads it: the parts separated by spaces, a part with a space in double quotes.
    /// </summary>
    public string CommandLine
        => string.Join(' ', Arguments.Prepend(FileName).Select(part => part.Contains(' ') ? $"\"{part}\"" : part));

    /// <summary>
    ///  The terminal used when the user has not configured one: Terminal on macOS, cmd on Windows, and the
    ///  <c>TERMINAL</c> environment variable or <c>x-terminal-emulator</c> elsewhere.
    /// </summary>
    public static TerminalCommand Default(HostPlatform platform, string? terminalEnvironmentValue)
    {
        if (platform.IsWindows)
        {
            return new TerminalCommand("cmd.exe", []);
        }

        if (platform.IsMacOS)
        {
            return new TerminalCommand("open", ["-a", "Terminal", FolderToken]);
        }

        string fileName = string.IsNullOrWhiteSpace(terminalEnvironmentValue) ? "x-terminal-emulator" : terminalEnvironmentValue.Trim();
        return new TerminalCommand(fileName, []);
    }

    /// <summary>
    ///  The terminal command the user configured (Settings, Paths, on Linux and macOS): the program, then its arguments,
    ///  separated by spaces, with double quotes around a part that has spaces; null when it is blank, so the default is used.
    /// </summary>
    public static TerminalCommand? Parse(string? commandLine)
    {
        List<string> parts = [];
        System.Text.StringBuilder part = new();
        bool quoted = false;
        bool started = false;
        foreach (char c in commandLine ?? "")
        {
            if (c == '"')
            {
                quoted = !quoted;
                started = true;
            }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                if (started)
                {
                    parts.Add(part.ToString());
                    part.Clear();
                    started = false;
                }
            }
            else
            {
                part.Append(c);
                started = true;
            }
        }

        if (started)
        {
            parts.Add(part.ToString());
        }

        return parts is [var fileName, .. var arguments] && fileName.Length > 0
            ? new TerminalCommand(fileName, arguments)
            : null;
    }
}

public interface ITerminalLauncher
{
    /// <summary>
    ///  Opens a terminal whose working directory is <paramref name="folder"/>.
    /// </summary>
    void OpenTerminal(string folder);
}

public sealed class SystemTerminalLauncher(IProcessLauncher launcher, TerminalCommand command) : ITerminalLauncher
{
    public void OpenTerminal(string folder)
    {
        List<string> arguments = command.Arguments
            .Select(argument => argument.Replace(TerminalCommand.FolderToken, folder, StringComparison.Ordinal))
            .ToList();

        launcher.Start(command.FileName, arguments, folder);
    }
}
