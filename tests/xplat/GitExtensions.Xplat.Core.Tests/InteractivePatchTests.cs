using AwesomeAssertions;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Platform;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class InteractivePatchTests
{
    [Test]
    public void GitArguments_should_be_upstreams_interactive_add_and_reset()
    {
        InteractivePatch.GitArguments("dir/a b.txt", reset: false).ToString().Should().Be("add --patch \"dir/a b.txt\"");
        InteractivePatch.GitArguments("dir/a b.txt", reset: true).ToString().Should().Be("checkout -p \"dir/a b.txt\"");
    }

    [Test]
    public void TerminalCommand_should_run_git_after_the_terminals_execute_option()
    {
        InteractivePatch.TerminalCommand(null, "/usr/bin/git", InteractivePatch.GitArguments("a.txt", reset: false))
            .Should().Be(("x-terminal-emulator", "-e \"/usr/bin/git\" add --patch \"a.txt\""));
        InteractivePatch.TerminalCommand(" xterm ", "git", InteractivePatch.GitArguments("a.txt", reset: true))
            .Should().Be(("xterm", "-e \"git\" checkout -p \"a.txt\""), "the TERMINAL variable names the terminal");
    }

    [Test]
    public void IsAvailable_should_leave_out_macOS()
    {
        InteractivePatch.IsAvailable(new HostPlatform(IsWindows: true, IsMacOS: false)).Should().BeTrue();
        InteractivePatch.IsAvailable(new HostPlatform(IsWindows: false, IsMacOS: false)).Should().BeTrue();
        InteractivePatch.IsAvailable(new HostPlatform(IsWindows: false, IsMacOS: true)).Should().BeFalse();
    }
}
