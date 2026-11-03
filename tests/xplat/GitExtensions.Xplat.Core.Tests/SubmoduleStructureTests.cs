using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class SubmoduleStructureTests
{
    private static readonly SubmoduleProject _top = new("top (main)", "/w/top/", IsCurrent: false, "", "/w/top/");
    private static readonly SubmoduleProject _a = new("a (main)", "/w/top/a/", IsCurrent: false, "a", "/w/top/");
    private static readonly SubmoduleProject _b = new("a/b (main)", "/w/top/a/b/", IsCurrent: true, "b", "/w/top/a/");

    [Test]
    public void MenuItems_should_list_the_submodules_below_the_top_project_then_update_all()
    {
        SubmoduleStructure structure = new(_top with { IsCurrent = true }, [_a, _b], IsBareRepository: false)
        {
            OurSubmodules = [_a with { AddedAndRemoved = " (+1-0)" }, _b with { IsCurrent = false }],
        };

        structure.MenuItems(text => $"<{text}>").Select(item => (item.Kind, item.Text)).Should().Equal(
            (SubmoduleMenuItemKind.Project, "a (main) (+1-0)"),
            (SubmoduleMenuItemKind.Project, "a/b (main)"),
            (SubmoduleMenuItemKind.Separator, ""),
            (SubmoduleMenuItemKind.UpdateAll, "<&Update all submodules>"));
        (structure with { OurSubmodules = [] }).MenuItems(text => text).Select(item => item.Kind).Should().Equal(
            SubmoduleMenuItemKind.NoSubmodules, SubmoduleMenuItemKind.Separator, SubmoduleMenuItemKind.UpdateAll);
    }

    [Test]
    public void MenuItems_should_add_the_top_project_the_superproject_and_all_submodules_inside_a_submodule()
    {
        SubmoduleStructure structure = new(_top, [_a, _b], IsBareRepository: false)
        {
            AllSubmodules = [_a, _b],
            SuperProject = _a,
            CurrentSubmoduleName = "b",
        };

        IReadOnlyList<SubmoduleMenuItem> items = structure.MenuItems(text => text);

        items.Select(item => (item.Kind, item.Text)).Should().Equal(
            (SubmoduleMenuItemKind.NoSubmodules, "No submodules"),
            (SubmoduleMenuItemKind.Separator, ""),
            (SubmoduleMenuItemKind.Project, "Top project: top (main)"),
            (SubmoduleMenuItemKind.Project, "Superproject: a (main)"),
            (SubmoduleMenuItemKind.Project, "a (main)"),
            (SubmoduleMenuItemKind.Project, "a/b (main)"),
            (SubmoduleMenuItemKind.Separator, ""),
            (SubmoduleMenuItemKind.UpdateAll, "&Update all submodules"),
            (SubmoduleMenuItemKind.UpdateCurrent, "Update current submodule"));
        items[2].Project.Should().Be(_top);
        (structure with { SuperProject = _top }).MenuItems(text => text).Select(item => item.Text)
            .Should().NotContain(text => text.StartsWith("Top project", StringComparison.Ordinal),
                "upstream names the top project once when it is the superproject");
    }

    [Test]
    public void ChangeSummary_should_group_the_changes_as_upstreams_ArtificialCommitChangeCount_listing_five_of_each()
    {
        List<GitItemStatus> items =
        [
            .. Enumerable.Range(1, 7).Select(i => new GitItemStatus($"f{i}.txt") { IsChanged = true }),
            new("gone.txt") { IsDeleted = true },
            new("lib") { IsSubmodule = true, IsChanged = true, IsDirty = true },
        ];

        SubmoduleStructure.ChangeSummary(items).ReplaceLineEndings("\n").Should().Be(
            "7 changed files\n- f1.txt\n- f2.txt\n- f3.txt\n- f4.txt\n- f5.txt\n- (2 more files)\n\n"
            + "1 deleted file\n- gone.txt\n\n"
            + "1 changed submodule\n- lib\n\n"
            + "1 dirty submodule\n- lib\n");
        SubmoduleStructure.ChangeSummary([]).Should().BeEmpty();
    }
}
