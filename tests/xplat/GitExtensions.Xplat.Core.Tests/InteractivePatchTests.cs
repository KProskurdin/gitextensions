using System.Diagnostics;
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
    public void MacScript_should_quote_for_the_shell_and_always_leave_an_exit_code()
    {
        string script = InteractivePatch.MacScript("/work dir", "/usr/bin/git", "a$b 'c'.txt", reset: true, "/tmp/code");

        script.Should().StartWith("#!/bin/sh\n");
        script.Should().Contain("cd '/work dir' ||");
        script.Should().Contain(@"'/usr/bin/git' checkout -p 'a$b '\''c'\''.txt'");
        script.Should().Contain("HUP", "closing the window must not leave the app waiting");
    }

    [Test]
    [Platform(Include = "Linux,MacOsX")]
    public void MacScript_should_stage_the_answered_change_of_an_awkward_file_name()
    {
        string folder = Directory.CreateTempSubdirectory("xplat-patch-").FullName;
        try
        {
            const string fileName = "a$b 'c'.txt";
            Git(folder, "init", "-q");
            Git(folder, "config", "user.name", "Test");
            Git(folder, "config", "user.email", "test@example.com");
            File.WriteAllText(Path.Combine(folder, fileName), "one\n");
            Git(folder, "add", "-A");
            Git(folder, "commit", "-q", "-m", "first");
            File.WriteAllText(Path.Combine(folder, fileName), "two\n");
            string exitCode = Path.Combine(folder, ".git", "exit-code");
            string script = Path.Combine(folder, ".git", "patch.command");
            File.WriteAllText(script, InteractivePatch.MacScript(folder, "git", fileName, reset: false, exitCode));

            using Process shell = Process.Start(new ProcessStartInfo("sh", [script]) { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false })!;
            shell.StandardInput.WriteLine("y");
            shell.StandardInput.Close();
            _ = shell.StandardOutput.ReadToEnd();
            shell.WaitForExit();

            File.ReadAllText(exitCode).Trim().Should().Be("0");
            Git(folder, "diff", "--cached", "--name-only", "-z").Should().Be(fileName + "\0");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static string Git(string folder, params string[] arguments)
    {
        using Process git = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        })!;
        string output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        git.ExitCode.Should().Be(0);
        return output;
    }
}
