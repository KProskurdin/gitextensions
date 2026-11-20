using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// Upstream's GitUICommands.UninstallEditor, the command line's uninstall verb.
internal sealed class GitEditorUninstallTests
{
    private const string InstallDir = "/Applications/Git Extensions.app/Contents/MacOS";

    private IGitConfigService _config = null!;

    [SetUp]
    public void Setup() => _config = Substitute.For<IGitConfigService>();

    [Test]
    public async Task RunAsync_should_remove_an_editor_that_names_the_install_folder_ignoring_case()
    {
        _config.GetAsync(ConfigScope.Global, "core.editor").Returns($"\"{InstallDir.ToUpperInvariant()}/GitExtensions\" fileeditor");

        (await GitEditorUninstall.RunAsync(_config, InstallDir)).Should().BeTrue();

        await _config.Received(1).SetAsync(ConfigScope.Global, "core.editor", "");
    }

    [Test]
    public async Task RunAsync_should_leave_another_editor()
    {
        _config.GetAsync(ConfigScope.Global, "core.editor").Returns("vim");

        (await GitEditorUninstall.RunAsync(_config, InstallDir)).Should().BeFalse();

        await _config.DidNotReceiveWithAnyArgs().SetAsync(default, default!, default!);
    }

    [Test]
    public async Task RunAsync_should_leave_the_editor_without_an_install_folder()
    {
        (await GitEditorUninstall.RunAsync(_config, installDir: null)).Should().BeFalse();

        await _config.DidNotReceiveWithAnyArgs().GetAsync(default, default!);
    }
}
