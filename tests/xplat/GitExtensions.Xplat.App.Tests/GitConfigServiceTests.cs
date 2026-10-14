using System.Diagnostics;
using Avalonia.Headless.NUnit;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Runs git in a temporary repository (AvaloniaTest runs the app, which sets ThreadHelper.JoinableTaskContext), with the
// global config in a temporary file and no system config, so the user's settings are never read or written.
internal sealed class GitConfigServiceTests
{
    private string _folder = null!;
    private string? _globalConfig;
    private string? _noSystem;

    [SetUp]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), "xplat-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _globalConfig = Environment.GetEnvironmentVariable("GIT_CONFIG_GLOBAL");
        _noSystem = Environment.GetEnvironmentVariable("GIT_CONFIG_NOSYSTEM");
        Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", Path.Combine(_folder, "global.gitconfig"));
        Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", "1");
        Git("init", "-q", "repo");
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", _globalConfig);
        Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", _noSystem);
        foreach (string file in Directory.EnumerateFiles(_folder, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_folder, recursive: true);
    }

    [AvaloniaTest]
    public async Task GetAsync_should_read_the_effective_value_as_git_applies_it()
    {
        GitConfigService config = new();
        string repository = Path.Combine(_folder, "repo");
        await config.SetAsync(ConfigScope.Global, "user.name", "Global Name");

        (await config.GetAsync(ConfigScope.Effective, "user.name", repository)).Should().Be("Global Name");

        await config.SetAsync(ConfigScope.Local, "user.name", "Local Name", repository);

        (await config.GetAsync(ConfigScope.Effective, "user.name", repository)).Should().Be("Local Name");
        (await config.GetAsync(ConfigScope.Global, "user.name")).Should().Be("Global Name");
        (await config.GetAsync(ConfigScope.System, "user.name")).Should().BeEmpty();
    }

    [TestCase(ConfigScope.Effective)]
    [TestCase(ConfigScope.System)]
    public void SetAsync_should_refuse_the_read_only_scopes(ConfigScope scope)
    {
        Func<Task> set = () => new GitConfigService().SetAsync(scope, "user.name", "x", Path.Combine(_folder, "repo"));

        set.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private void Git(params string[] arguments)
    {
        ProcessStartInfo start = new("git") { WorkingDirectory = _folder, UseShellExecute = false };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        process.WaitForExit();
        process.ExitCode.Should().Be(0);
    }
}
