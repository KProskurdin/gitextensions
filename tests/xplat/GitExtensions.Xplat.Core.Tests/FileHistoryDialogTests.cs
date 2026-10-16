using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class FileHistoryDialogTests
{
    [Test]
    public void The_texts_are_upstreams()
    {
        string source = UpstreamSource("src/app/GitUI/CommandsDialogs/FormFileHistory.cs");
        source.Should().Contain($"\"{FileHistoryDialog.TitlePrefix}\"").And
            .Contain($"\"{FileHistoryDialog.FileNotFound}\"");
        UpstreamSource("src/app/GitUI/CommandsDialogs/FormFileHistory.Designer.cs").Should()
            .Contain($"CommitInfoTabPage.Text = \"{FileHistoryDialog.CommitTabText}\"");
    }

    [Test]
    public void The_title_names_the_file_its_name_in_the_commit_and_the_repository()
    {
        FileHistoryDialog dialog = new("\"src\\a.txt\"");

        dialog.FileName.Should().Be("src/a.txt");
        dialog.Title("~/repo").Should().Be("File History - src/a.txt - ~/repo");
        dialog.Title("~/repo", "src/a.txt").Should().Be("File History - src/a.txt - ~/repo");
        dialog.Title("~/repo", "old/a.txt").Should().Be("File History - src/a.txt (old/a.txt) - ~/repo");
    }

    [Test]
    public void A_commit_shows_the_tabs_upstream_shows_for_it()
    {
        FileHistoryDialog dialog = new("a.txt");

        dialog.Tabs(isArtificial: false, fileAvailable: true, isFolder: false, "a.txt").Should().BeEquivalentTo(
            new FileHistoryTabs([FileHistoryTab.Commit, FileHistoryTab.Diff, FileHistoryTab.View, FileHistoryTab.Blame],
                null, "Commit"));
        dialog.Tabs(isArtificial: true, fileAvailable: true, isFolder: false, "a.txt").Should().BeEquivalentTo(
            new FileHistoryTabs([FileHistoryTab.Diff], FileHistoryTab.Diff, "Commit"));
        dialog.Tabs(isArtificial: false, fileAvailable: false, isFolder: false, "a.txt").Should().BeEquivalentTo(
            new FileHistoryTabs([FileHistoryTab.Commit], FileHistoryTab.Commit,
                "Commit - Git could not identify the file \"a.txt\""));
        dialog.Tabs(isArtificial: false, fileAvailable: false, isFolder: true, "src/").CommitCaption.Should()
            .Be("Commit");

        new FileHistoryDialog("sub", isSubmodule: true).Tabs(false, true, false, "sub").Visible.Should()
            .NotContain(FileHistoryTab.Blame);
    }

    [Test]
    public void The_window_opens_on_blame_only_when_asked_and_there_is_one()
    {
        new FileHistoryDialog("a.txt").InitialTab(showBlame: true).Should().Be(FileHistoryTab.Blame);
        new FileHistoryDialog("a.txt").InitialTab(showBlame: false).Should().Be(FileHistoryTab.Diff);
        new FileHistoryDialog("sub", isSubmodule: true).InitialTab(showBlame: true).Should().Be(FileHistoryTab.Diff);
    }

    [Test]
    public void The_history_loads_on_show_as_the_settings_say()
    {
        FileHistoryOptions none = new(LoadHistoryOnShow: false, LoadBlameOnShow: false);

        FileHistoryDialog.LoadsOnShow(FileHistoryTab.Diff, none).Should().BeFalse();
        FileHistoryDialog.LoadsOnShow(FileHistoryTab.Blame, none with { LoadBlameOnShow = true }).Should().BeTrue();
        FileHistoryDialog.LoadsOnShow(FileHistoryTab.Diff, none with { LoadBlameOnShow = true }).Should().BeFalse();
        FileHistoryDialog.LoadsOnShow(FileHistoryTab.Diff, new FileHistoryOptions()).Should().BeTrue();
    }

    [Test]
    public void The_menu_items_apply_as_upstream_says()
    {
        FileHistoryDialog.Menu(1, firstIsArtificial: false, firstIsWorkTree: false, fileExistsInWorkTree: true).Should()
            .Be(new FileHistoryMenu(true, true, true, true, true));
        FileHistoryDialog.Menu(1, firstIsArtificial: true, firstIsWorkTree: true, fileExistsInWorkTree: true).Should()
            .Be(new FileHistoryMenu(false, true, false, true, false));
        FileHistoryDialog.Menu(0, false, false, true).Should()
            .Be(new FileHistoryMenu(false, false, false, false, false));
    }

    [Test]
    public void A_narrowed_history_is_read_in_full_when_asked()
    {
        new RevisionFilter(PathFilter: "a.txt", FullHistory: true, SimplifyMerges: true).ToRevisionArguments().Should()
            .Contain("--full-history --simplify-merges");
        new RevisionFilter(PathFilter: "a.txt", SimplifyMerges: true).ToRevisionArguments().Should()
            .NotContain("--full-history").And.NotContain("--simplify-merges");
        new RevisionFilter(FullHistory: true).ToRevisionArguments().Should().NotContain("--full-history",
            "nothing narrows the history");
    }

    private static string UpstreamSource(string relativePath,
        [System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => File.ReadAllText(
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", relativePath)));
}
