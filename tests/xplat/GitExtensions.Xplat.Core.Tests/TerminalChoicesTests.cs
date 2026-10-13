using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Platform;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class TerminalChoicesTests
{
    private static readonly HostPlatform Windows = new(IsWindows: true, IsMacOS: false);
    private static readonly HostPlatform MacOs = new(IsWindows: false, IsMacOS: true);
    private static readonly HostPlatform Linux = new(IsWindows: false, IsMacOS: false);

    [Test]
    public void For_should_list_upstreams_installed_shells_in_its_order_on_windows()
    {
        IReadOnlyList<TerminalChoice> choices = TerminalChoices.For(Windows, null,
            Finder("git-bash.exe", "bash.exe", "cmd.exe", "powershell.exe"));

        choices.Select(choice => (choice.Name, choice.Image, choice.Command.FileName)).Should().Equal(
            ("bash", "GitForWindows", @"C:\shells\git-bash.exe"),
            ("cmd", "cmd", @"C:\shells\cmd.exe"),
            ("powershell", "powershell", @"C:\shells\powershell.exe"));
    }

    [Test]
    public void For_should_fall_back_to_bash_then_sh_as_upstreams_BashShell()
    {
        TerminalChoices.For(Windows, null, Finder("sh.exe")).Should().ContainSingle()
            .Which.Command.FileName.Should().Be(@"C:\shells\sh.exe");
    }

    [Test]
    public void For_should_offer_the_system_terminal_elsewhere()
    {
        TerminalChoices.For(Linux, "gnome-terminal", Finder()).Should().ContainSingle()
            .Which.Should().Be(new TerminalChoice("gnome-terminal", "Console",
                TerminalCommand.Default(Linux, "gnome-terminal")));
        TerminalChoices.For(MacOs, null, Finder()).Should().ContainSingle()
            .Which.Name.Should().Be("Terminal");
    }

    [Test]
    public void Default_should_pick_the_named_shell_or_else_the_first()
    {
        IReadOnlyList<TerminalChoice> choices = TerminalChoices.For(Windows, null, Finder("cmd.exe", "pwsh.exe"));

        TerminalChoices.Default(choices, "PWSH")!.Name.Should().Be("pwsh");
        TerminalChoices.Default(choices, TerminalChoices.DefaultName)!.Name.Should().Be("cmd");
        TerminalChoices.Default([], TerminalChoices.DefaultName).Should().BeNull();
    }

    private static TerminalChoices.ShellFinder Finder(params string[] installed)
        => (string executable, [NotNullWhen(true)] out string? path) =>
        {
            path = installed.Contains(executable) ? @"C:\shells\" + executable : null;
            return path is not null;
        };
}
