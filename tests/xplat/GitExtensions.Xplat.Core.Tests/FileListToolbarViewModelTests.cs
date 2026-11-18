using AwesomeAssertions;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// The file lists' shared toolbar and menus: the diff status filter and collapse button, the toolbar's item choices, the
// copy paths items and the diff tool choices.
internal sealed class FileListToolbarViewModelTests
{
    [Test]
    public void The_diff_status_filter_should_hide_the_status_unchecked_and_tell_the_list()
    {
        FileListToolbarViewModel toolbar = new(new InMemoryAppPreferences());
        int changed = 0;
        toolbar.Changed += (_, _) => changed++;
        toolbar.Shows(DiffBranchStatus.OnlyAChange).Should().BeTrue();

        toolbar.ToggleDiffStatusCommand.Execute(DiffBranchStatus.OnlyAChange);

        toolbar.Shows(DiffBranchStatus.OnlyAChange).Should().BeFalse();
        toolbar.Shows(DiffBranchStatus.OnlyBChange).Should().BeTrue();
        toolbar.Shows(DiffBranchStatus.Unknown).Should().BeTrue("files outside BASE groups are always shown");
        changed.Should().Be(1);
    }

    [Test]
    public void The_grouping_and_the_collapse_button_should_reach_the_list()
    {
        FileListToolbarViewModel toolbar = new(new InMemoryAppPreferences());
        List<string> raised = [];
        toolbar.Changed += (_, _) => raised.Add("changed");
        toolbar.CollapseGroupsRequested += (_, _) => raised.Add("collapse");

        toolbar.Grouping.ToggleTreeCommand.Execute(null);
        toolbar.CollapseGroupsCommand.Execute(null);

        raised.Should().Equal("changed", "collapse");
    }

    [Test]
    public void A_toolbar_item_unchecked_should_be_stored_hidden_and_the_settings_button_should_stay()
    {
        InMemoryAppPreferences preferences = new();
        FileListToolbarChoicesViewModel choices = new(preferences,
        [
            new FileListToolbarChoice("btnByPath", "Group by file path", "FolderClosed", IsSettingsButton: false),
            new FileListToolbarChoice("btnSettings", "Settings", "Settings", IsSettingsButton: true),
        ]);
        List<(string, bool)> shown = [];
        choices.ItemShown += (_, item) => shown.Add(item);

        MenuEntry menu = choices.MenuEntry("_Toolbar");
        menu.Children.Select(entry => (entry.Text, entry.IsChecked, entry.IsEnabled)).Should()
            .Equal(("Group by file path", true, true), ("Settings", true, false));
        menu.Children[0].Command!.Execute(menu.Children[0].CommandParameter);

        preferences.IsFileListToolbarItemVisible("btnByPath").Should().BeFalse();
        shown.Should().Equal(("btnByPath", false));
        choices.MenuEntry("_Toolbar").Children[0].IsChecked.Should().BeFalse();
    }

    [TestCase(CopyPathsKind.RelativePosix, "docs/a.md")]
    [TestCase(CopyPathsKind.FullNative, "/repo/docs/a.md")]
    public async Task Copy_paths_should_copy_each_path_once_as_the_item_says(CopyPathsKind kind, string expected)
    {
        List<string> copied = [];
        CopyPathsMenuViewModel menu = new(() => ["docs/a.md", "docs/a.md"], () => "/repo", text =>
        {
            copied.Add(text);
            return Task.CompletedTask;
        });

        IReadOnlyList<MenuEntry> entries = menu.Entries();
        await ((IAsyncRelayCommand<CopyPathsKind>)entries[0].Command!).ExecuteAsync(kind);

        entries.Should().HaveCount(5);
        entries.Single(entry => entry.IsBold).CommandParameter.Should().Be(CopyPathsKind.FullNative);
        copied.Should().Equal(OperatingSystem.IsWindows() ? expected.Replace('/', '\\') : expected);
    }

    [Test]
    public async Task A_diff_tool_choice_should_open_its_tool_and_show_a_failure()
    {
        List<string> opened = [];
        List<string> errors = [];
        DiffToolChoicesViewModel choices = new(() => Task.FromResult<IReadOnlyList<string>>(["meld", "kdiff3"]), tool =>
        {
            opened.Add(tool);
            return tool == "kdiff3" ? Task.FromException(new InvalidOperationException("not installed")) : Task.CompletedTask;
        }, errors.Add);

        IReadOnlyList<MenuEntry> entries = await choices.EntriesAsync();
        foreach (MenuEntry entry in entries)
        {
            await ((IAsyncRelayCommand<string>)entry.Command!).ExecuteAsync((string)entry.CommandParameter!);
        }

        entries.Select(entry => entry.Text).Should().Equal("meld", "kdiff3");
        opened.Should().Equal("meld", "kdiff3");
        errors.Should().Equal("not installed");
    }
}
