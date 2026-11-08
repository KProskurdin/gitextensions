using GitExtensions.Xplat.Core.Platform;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

/// <summary>
///  The decisions behind fitting into macOS. They are plain rules, so they run on every OS, except where a real shell runs.
/// </summary>
internal sealed class MacOSIntegrationTests
{
    private static readonly TimeSpan _shellTimeout = TimeSpan.FromSeconds(20);

    [TestCase(true, null, true)]
    [TestCase(true, "", true)]
    [TestCase(true, "xterm-256color", false)]
    [TestCase(false, null, false)]
    public void ShouldRead_should_ask_only_on_macOS_without_a_terminal(bool isMacOS, string? terminal, bool expected)
    {
        LoginShellPath.ShouldRead(isMacOS, terminal).Should().Be(expected);
    }

    [Test]
    public void Merge_should_put_the_login_folders_first_and_keep_the_others_once()
    {
        char separator = Path.PathSeparator;

        LoginShellPath.Merge($"/opt/homebrew/bin{separator}/usr/bin", $"/usr/bin{separator}/bin")
            .Should().Be($"/opt/homebrew/bin{separator}/usr/bin{separator}/bin");
        LoginShellPath.Merge(null, "/bin").Should().Be("/bin");
    }

    [Test]
    [Platform(Include = "Linux,MacOsX")]
    public void Read_should_take_the_last_line_after_a_greeting()
    {
        WithShell("echo 'Welcome!'; echo; echo /a:/b", shell => LoginShellPath.Read(shell, _shellTimeout).Should().Be("/a:/b"));
    }

    [Test]
    [Platform(Include = "Linux,MacOsX")]
    public void Read_should_give_up_on_a_failing_or_slow_shell()
    {
        WithShell("exit 3", shell => LoginShellPath.Read(shell, _shellTimeout).Should().BeNull());
        WithShell("sleep 30", shell => LoginShellPath.Read(shell, TimeSpan.FromMilliseconds(300)).Should().BeNull());
        LoginShellPath.Read("/no/such/shell", _shellTimeout).Should().BeNull();
    }

    [Test]
    [Platform(Include = "Linux,MacOsX")]
    public void Read_should_work_with_a_real_shell()
    {
        LoginShellPath.Read("/bin/sh", _shellTimeout).Should().Contain("/usr/bin");
    }

    [Test]
    public void NewInstance_should_use_open_for_a_bundle_on_macOS_only()
    {
        const string bundled = "/Applications/Git Extensions.app/Contents/MacOS/GitExtensions";

        NewInstanceCommand.For(bundled, "/repo", isMacOS: true).Arguments
            .Should().Equal("-n", "/Applications/Git Extensions.app", "--args", "/repo");
        NewInstanceCommand.For(bundled, "/repo", isMacOS: true).FileName.Should().Be("open");
    }

    [Test]
    public void NewInstance_should_start_the_executable_outside_a_bundle()
    {
        NewInstanceCommand.For("/build/GitExtensions", "/repo", isMacOS: true).FileName.Should().Be("/build/GitExtensions");
        NewInstanceCommand.For("/build/GitExtensions", "/repo", isMacOS: true).Arguments.Should().Equal("/repo");
        NewInstanceCommand.For("/x.app/Contents/MacOS/GitExtensions", "/repo", isMacOS: false).FileName
            .Should().Be("/x.app/Contents/MacOS/GitExtensions");
    }

    [Test]
    public void OpenedItem_should_be_a_folder_itself_or_the_folder_of_a_file()
    {
        OpenedItem.Folder("/repo", _ => true).Should().Be("/repo");
        OpenedItem.Folder(Path.Combine("repo", "a.txt"), _ => false).Should().Be("repo");
    }

    // A stand-in login shell: it ignores its options and runs the body, as a shell's configuration would before the command.
    private static void WithShell(string body, Action<string> test)
    {
        string folder = Directory.CreateTempSubdirectory("xplat-shell-").FullName;
        try
        {
            string shell = Path.Combine(folder, "shell");
            File.WriteAllText(shell, $"#!/bin/sh\n{body}\n");
            File.SetUnixFileMode(shell, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            test(shell);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
