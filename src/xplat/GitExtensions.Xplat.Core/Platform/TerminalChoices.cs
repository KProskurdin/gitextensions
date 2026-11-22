using System.Diagnostics.CodeAnalysis;

namespace GitExtensions.Xplat.Core.Platform;

/// <summary>
///  A terminal the browse toolbar's terminal button opens: the new shell's version of upstream's <c>IShellDescriptor</c>.
/// </summary>
/// <param name="Name">The name upstream shows in the drop-down and the tooltip.</param>
/// <param name="Image">Upstream's image name (<c>GitUI.Properties.Images</c>).</param>
/// <param name="Command">The program that opens the terminal.</param>
public sealed record TerminalChoice(string Name, string Image, TerminalCommand Command);

/// <summary>
///  The terminals of upstream's terminal drop-down (<c>ShellProvider</c> and <c>FormBrowse.FillUserShells</c>): on
///  Windows upstream's shells that are installed, elsewhere the system's terminal.
/// </summary>
public static class TerminalChoices
{
    /// <summary>
    ///  Upstream's <c>BashShell.ShellName</c>, the shell <c>FillUserShells</c> selects when it is installed.
    /// </summary>
    public const string DefaultName = "bash";

    // Upstream's BashShell looks for these in this order; the others have one executable each.
    private static readonly string[] _bashExecutables = ["git-bash.exe", "bash.exe", "sh.exe"];

    /// <summary>
    ///  Finds a shell's executable as upstream's <c>PathUtil.TryFindShellPath</c> does.
    /// </summary>
    public delegate bool ShellFinder(string executableName, [NotNullWhen(true)] out string? path);

    /// <summary>
    ///  The terminals in upstream's order (bash, cmd, pwsh, powershell), leaving out those not installed. Off Windows, the
    ///  terminal <paramref name="configured"/> names (Settings, Paths; decided with the user on 2026-11-22), else the
    ///  system's.
    /// </summary>
    public static IReadOnlyList<TerminalChoice> For(HostPlatform platform, string? terminalEnvironmentValue, ShellFinder find,
        string? configured = null)
    {
        if (!platform.IsWindows)
        {
            if (TerminalCommand.Parse(configured) is { } own)
            {
                return [new TerminalChoice(Path.GetFileName(own.FileName), "Console", own)];
            }

            TerminalCommand command = TerminalCommand.Default(platform, terminalEnvironmentValue);
            string name = platform.IsMacOS ? "Terminal" : Path.GetFileName(command.FileName);
            return [new TerminalChoice(name, "Console", command)];
        }

        List<TerminalChoice> choices = [];
        foreach (string bash in _bashExecutables)
        {
            if (find(bash, out string? path))
            {
                choices.Add(new TerminalChoice(DefaultName, "GitForWindows", new TerminalCommand(path, [])));
                break;
            }
        }

        Add("cmd", "cmd", "cmd.exe");
        Add("pwsh", "pwsh", "pwsh.exe");
        Add("powershell", "powershell", "powershell.exe");
        return choices;

        void Add(string name, string image, string executable)
        {
            if (find(executable, out string? path))
            {
                choices.Add(new TerminalChoice(name, image, new TerminalCommand(path, [])));
            }
        }
    }

    /// <summary>
    ///  The terminal the button opens: the one named <paramref name="name"/>, or the first one when it is not installed.
    /// </summary>
    public static TerminalChoice? Default(IReadOnlyList<TerminalChoice> choices, string name)
        => choices.FirstOrDefault(choice => string.Equals(choice.Name, name, StringComparison.InvariantCultureIgnoreCase))
           ?? choices.FirstOrDefault();
}
