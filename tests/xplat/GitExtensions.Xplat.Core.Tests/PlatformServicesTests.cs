using AwesomeAssertions;
using GitExtensions.Xplat.Core.Platform;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class PlatformServicesTests
{
    private static readonly HostPlatform Windows = new(IsWindows: true, IsMacOS: false);
    private static readonly HostPlatform MacOs = new(IsWindows: false, IsMacOS: true);
    private static readonly HostPlatform Linux = new(IsWindows: false, IsMacOS: false);

    private const string FolderPath = "/work/repo";

    [TestCase(true, false, "explorer.exe")]
    [TestCase(false, true, "open")]
    [TestCase(false, false, "xdg-open")]
    public void OpenFolder_should_use_the_file_manager_of_each_platform(bool isWindows, bool isMacOS,
        string expectedFileName)
    {
        FakeProcessLauncher launcher = new();

        new SystemFileManager(launcher, new HostPlatform(isWindows, isMacOS)).OpenFolder(FolderPath);

        launcher.Started.Should().ContainSingle()
            .Which.Should().Be(new StartedProcess(expectedFileName, Args(FolderPath), FolderPath));
    }

    [Test]
    public void Default_terminal_on_windows_is_cmd()
    {
        TerminalCommand command = TerminalCommand.Default(Windows, terminalEnvironmentValue: null);

        command.FileName.Should().Be("cmd.exe");
        command.Arguments.Should().BeEmpty();
    }

    [Test]
    public void Default_terminal_on_macos_opens_the_folder_in_Terminal()
    {
        TerminalCommand command = TerminalCommand.Default(MacOs, terminalEnvironmentValue: null);

        command.FileName.Should().Be("open");
        command.Arguments.Should().Equal("-a", "Terminal", TerminalCommand.FolderToken);
    }

    [Test]
    public void Default_terminal_on_linux_uses_the_TERMINAL_variable_when_set()
    {
        TerminalCommand command = TerminalCommand.Default(Linux, terminalEnvironmentValue: " gnome-terminal ");

        command.FileName.Should().Be("gnome-terminal");
    }

    [Test]
    public void Default_terminal_on_linux_falls_back_to_x_terminal_emulator_when_TERMINAL_is_blank()
    {
        TerminalCommand command = TerminalCommand.Default(Linux, terminalEnvironmentValue: "  ");

        command.FileName.Should().Be("x-terminal-emulator");
    }

    [Test]
    public void OpenTerminal_replaces_the_folder_token_in_each_argument()
    {
        FakeProcessLauncher launcher = new();
        TerminalCommand command = new("open", ["-a", "Terminal", TerminalCommand.FolderToken]);

        new SystemTerminalLauncher(launcher, command).OpenTerminal(FolderPath);

        launcher.Started.Should().ContainSingle()
            .Which.Should().Be(new StartedProcess("open", Args("-a", "Terminal", FolderPath), FolderPath));
    }

    [Test]
    public void OpenTerminal_starts_the_terminal_in_the_folder_when_no_arguments_are_given()
    {
        FakeProcessLauncher launcher = new();

        new SystemTerminalLauncher(launcher, new TerminalCommand("cmd.exe", [])).OpenTerminal(FolderPath);

        launcher.Started.Should().ContainSingle()
            .Which.Should().Be(new StartedProcess("cmd.exe", Args(), FolderPath));
    }

    // Arguments are compared as one string: a list would be compared by reference inside the record.
    private static string Args(params string[] arguments) => string.Join("|", arguments);

    private sealed record StartedProcess(string FileName, string Arguments, string WorkingDirectory);

    private sealed class FakeProcessLauncher : IProcessLauncher
    {
        public List<StartedProcess> Started { get; } = [];

        public void Start(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
            => Started.Add(new StartedProcess(fileName, Args([.. arguments]), workingDirectory));
    }
}
