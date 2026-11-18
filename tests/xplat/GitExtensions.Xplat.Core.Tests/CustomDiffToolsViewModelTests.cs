using AwesomeAssertions;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// Upstream's CustomDiffMergeToolProvider: the tools under each difftool item, the item itself opening git's default.
internal sealed class CustomDiffToolsViewModelTests
{
    private readonly List<string?> _opened = [];
    private InMemoryAppPreferences _preferences = null!;
    private IDiffMergeToolCatalog _catalog = null!;

    [SetUp]
    public void SetUp()
    {
        _opened.Clear();
        _preferences = new InMemoryAppPreferences { ShowAvailableDiffTools = true };
        _catalog = Substitute.For<IDiffMergeToolCatalog>();
        IReadOnlyList<string> tools = ["meld", "vscode"];
        _catalog.GetAvailableAsync(true, Arg.Any<CancellationToken>()).Returns(tools);
    }

    [Test]
    public async Task LoadAsync_should_list_the_tools_the_default_bold_then_the_disable_item_under_every_item()
    {
        CustomDiffToolsViewModel tools = New();
        CustomDiffToolMenu first = tools.Add(Open);
        CustomDiffToolMenu second = tools.Add(Open);

        await tools.LoadAsync();

        foreach (CustomDiffToolMenu menu in new[] { first, second })
        {
            menu.Entries.Select(entry => entry.IsSeparator ? "-" : entry.Text).Should()
                .Equal("meld", "vscode", "-", "Disable this dropdown");
            menu.Entries.Select(entry => entry.IsBold).Should().Equal(true, false, false, false);
            menu.Entries[0].Command.Should().BeSameAs(menu.OpenCommand);
            menu.Entries[1].CommandParameter.Should().Be("vscode");
        }
    }

    [Test]
    public async Task OpenCommand_should_open_the_chosen_tool_or_the_default_one()
    {
        CustomDiffToolsViewModel tools = New();
        CustomDiffToolMenu menu = tools.Add(Open);
        await tools.LoadAsync();

        await menu.OpenCommand.ExecuteAsync(null);
        MenuEntry vscode = menu.Entries[1];
        vscode.Command!.Execute(vscode.CommandParameter);

        _opened.Should().Equal(null, "vscode");
    }

    [Test]
    public async Task DisableCommand_should_turn_the_setting_off_and_empty_every_item()
    {
        CustomDiffToolsViewModel tools = New();
        CustomDiffToolMenu first = tools.Add(Open);
        CustomDiffToolMenu second = tools.Add(Open);
        await tools.LoadAsync();

        first.Entries[^1].Command!.Execute(null);

        _preferences.ShowAvailableDiffTools.Should().BeFalse();
        _preferences.SaveCount.Should().Be(1);
        first.Entries.Should().BeEmpty();
        second.Entries.Should().BeEmpty();
    }

    [Test]
    public async Task LoadAsync_should_offer_nothing_while_the_setting_is_off_or_there_is_one_tool()
    {
        _preferences.ShowAvailableDiffTools = false;
        CustomDiffToolsViewModel off = New();
        CustomDiffToolMenu offMenu = off.Add(Open);
        await off.LoadAsync();
        offMenu.Entries.Should().BeEmpty();
        await _catalog.DidNotReceiveWithAnyArgs().GetAvailableAsync(default);

        _preferences.ShowAvailableDiffTools = true;
        IReadOnlyList<string> one = ["meld"];
        _catalog.GetAvailableAsync(true, Arg.Any<CancellationToken>()).Returns(one);
        CustomDiffToolsViewModel single = New();
        CustomDiffToolMenu singleMenu = single.Add(Open);
        await single.LoadAsync();
        singleMenu.Entries.Should().BeEmpty();
    }

    [Test]
    public async Task LoadAsync_should_read_the_tools_once()
    {
        CustomDiffToolsViewModel tools = New();
        tools.Add(Open);

        await tools.LoadAsync();
        await tools.LoadAsync();

        await _catalog.Received(1).GetAvailableAsync(true, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task LoadAsync_should_list_merge_tools_without_the_disable_item()
    {
        IReadOnlyList<string> mergeTools = ["kdiff3", "meld"];
        _catalog.GetAvailableAsync(false, Arg.Any<CancellationToken>()).Returns(mergeTools);
        _preferences.ShowAvailableDiffTools = false;
        CustomDiffToolsViewModel tools = new(_preferences, _catalog, diff: false, () => "Disable this dropdown");
        CustomDiffToolMenu menu = tools.Add(Open);

        await tools.LoadAsync();

        menu.Entries.Select(entry => entry.Text).Should().Equal("kdiff3", "meld");
    }

    private CustomDiffToolsViewModel New() => new(_preferences, _catalog, diff: true, () => "Disable this dropdown");

    private Task Open(string? tool)
    {
        _opened.Add(tool);
        return Task.CompletedTask;
    }
}
