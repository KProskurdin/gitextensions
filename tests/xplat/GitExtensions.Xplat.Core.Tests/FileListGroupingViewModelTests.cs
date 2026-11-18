using AwesomeAssertions;
using GitCommands;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class FileListGroupingViewModelTests
{
    [Test]
    public void GroupBy_should_keep_the_flat_variant_store_it_and_tell_the_lists()
    {
        InMemoryAppPreferences preferences = new() { DiffListSorting = DiffListSortType.FilePathFlat };
        FileListGroupingViewModel grouping = new(preferences);
        int changed = 0;
        grouping.Changed += (_, _) => changed++;

        grouping.GroupByCommand.Execute(DiffListSortType.FileStatus);

        preferences.DiffListSorting.Should().Be(DiffListSortType.FileStatusFlat);
        (grouping.IsByPath, grouping.IsByStatus).Should().Be((false, true));
        preferences.SaveCount.Should().Be(1);
        changed.Should().Be(1);
    }

    [Test]
    public void ToggleTree_should_switch_between_tree_and_flat_list_and_the_options_should_follow()
    {
        InMemoryAppPreferences preferences = new() { DiffListSorting = DiffListSortType.FileExtension };
        FileListGroupingViewModel grouping = new(preferences);
        (grouping.TreeImage, grouping.CanDenseTree, grouping.CanShowGroupNodesInFlatList).Should()
            .Be(("FileTree", true, false));

        grouping.ToggleTreeCommand.Execute(null);

        preferences.DiffListSorting.Should().Be(DiffListSortType.FileExtensionFlat);
        (grouping.TreeImage, grouping.CanDenseTree, grouping.CanShowGroupNodesInFlatList).Should()
            .Be(("DocumentTree", false, true));

        grouping.SetSortingCommand.Execute(DiffListSortType.FilePathFlat);

        grouping.CanShowGroupNodesInFlatList.Should().BeFalse("upstream groups a flat list by extension or status only");
    }

    [Test]
    public void The_checked_grouping_clicked_again_should_be_raised_again_for_its_toggle_button()
    {
        FileListGroupingViewModel grouping = new(new InMemoryAppPreferences { DiffListSorting = DiffListSortType.FilePath });
        List<string?> raised = [];
        grouping.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        grouping.GroupByCommand.Execute(DiffListSortType.FilePath);

        raised.Should().ContainSingle().Which.Should().BeEmpty("an empty name refreshes every binding");
        grouping.IsByPath.Should().BeTrue();
    }

    [Test]
    public void Dense_tree_and_group_nodes_should_toggle_their_settings()
    {
        InMemoryAppPreferences preferences = new();
        FileListGroupingViewModel grouping = new(preferences);
        bool dense = grouping.DenseTree;
        bool groupNodes = grouping.ShowGroupNodesInFlatList;

        grouping.ToggleDenseTreeCommand.Execute(null);
        grouping.ToggleShowGroupNodesInFlatListCommand.Execute(null);

        (preferences.FileStatusMergeSingleItemWithFolder, preferences.FileStatusShowGroupNodesInFlatList).Should()
            .Be((!dense, !groupNodes));
        (grouping.DenseTree, grouping.ShowGroupNodesInFlatList).Should().Be((!dense, !groupNodes));
    }
}
