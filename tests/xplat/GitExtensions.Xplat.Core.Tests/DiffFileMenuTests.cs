using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class DiffFileMenuTests
{
    private static readonly ObjectId Commit = ObjectId.Parse("1234567890123456789012345678901234567890");
    private static readonly ObjectId Parent = ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd");

    private string _workingDir = null!;

    [SetUp]
    public void SetUp()
    {
        _workingDir = Path.Combine(Path.GetTempPath(), "xplat-diffmenu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workingDir);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_workingDir, recursive: true);

    [Test]
    public void For_should_offer_a_commits_file_as_upstreams_menu_does()
    {
        DiffFileMenuState state = State(CommitSelection(new DiffMenuFile("M", "a.txt", null, IsTracked: true)));

        state.Stage.Should().BeFalse();
        state.Unstage.Should().BeFalse();
        state.ResetFile.Should().BeTrue();
        state.ResetToSecond.Should().Be("Second: B " + Commit.ToShortString());
        state.ResetToFirst.Should().Be("First: A " + Parent.ToShortString());
        state.OpenRevisionFile.Should().BeTrue();
        state.SaveAs.Should().BeTrue();
        state.DeleteFile.Should().BeFalse();
        state.FileHistory.Should().BeTrue();
        state.Blame.Should().BeTrue();
        state.ShowInFileTree.Should().BeTrue();
        state.AddToIgnore.Should().BeFalse();
        state.StopTracking.Should().BeTrue();
        state.DiffFirstToSelected.Should().BeTrue();
        state.ShowToLocal.Should().BeTrue();
    }

    [Test]
    public void For_should_not_offer_the_working_directory_file_when_it_does_not_exist()
    {
        DiffFileMenuState state = State(CommitSelection(new DiffMenuFile("M", "a.txt", null, IsTracked: true)));

        state.OpenWorkingFile.Should().BeFalse();
        state.EditWorkingFile.Should().BeFalse();
        state.DiffSelectedToLocal.Should().BeFalse();
        state.DiffFirstToLocal.Should().BeFalse();
    }

    [Test]
    public void For_should_offer_the_working_directory_file_when_it_exists()
    {
        File.WriteAllText(Path.Combine(_workingDir, "a.txt"), "a");

        DiffFileMenuState state = State(CommitSelection(new DiffMenuFile("M", "a.txt", null, IsTracked: true)));

        state.OpenWorkingFile.Should().BeTrue();
        state.EditWorkingFile.Should().BeTrue();
        state.DiffSelectedToLocal.Should().BeTrue();
        state.DiffFirstToLocal.Should().BeTrue();
    }

    [Test]
    public void For_should_offer_an_untracked_file_as_upstreams_menu_does()
    {
        File.WriteAllText(Path.Combine(_workingDir, "new.txt"), "n");

        DiffFileMenuState state = State(new DiffMenuSelection(ObjectId.IndexId, ObjectId.WorkTreeId, [],
            [new DiffMenuFile("?", "new.txt", null, IsTracked: false)]));

        state.Stage.Should().BeTrue();
        state.Unstage.Should().BeFalse();
        state.ResetToSecond.Should().BeNull();
        state.ResetToFirst.Should().Be("First: A Commit index");
        state.ResetFile.Should().BeFalse("upstream resets only tracked files");
        state.DeleteFile.Should().BeTrue();
        state.DeleteFileText.Should().Be(ResourceManager.TranslatedStrings.GetDeleteFile(1));
        state.AddToIgnore.Should().BeTrue();
        state.StopTracking.Should().BeFalse();
        state.OpenRevisionFile.Should().BeFalse();
        state.SaveAs.Should().BeFalse();
        state.FileHistory.Should().BeFalse();
        state.ShowToLocal.Should().BeFalse("the working directory compared to the index has no other local side");
    }

    [Test]
    public void For_should_offer_unstage_and_reset_to_HEAD_for_a_staged_file()
    {
        File.WriteAllText(Path.Combine(_workingDir, "a.txt"), "a");

        DiffFileMenuState state = State(new DiffMenuSelection(Parent, ObjectId.IndexId, [Parent],
            [new DiffMenuFile("M", "a.txt", null, IsTracked: true)]));

        state.Stage.Should().BeFalse();
        state.Unstage.Should().BeTrue();
        state.ResetToSecond.Should().BeNull();
        state.ResetToFirst.Should().Be("First: A " + Parent.ToShortString());
        state.ResetFile.Should().BeTrue();
        state.DeleteFile.Should().BeTrue();
        state.AddToIgnore.Should().BeFalse();
    }

    [Test]
    public void For_should_limit_single_file_items_to_one_selected_file()
    {
        File.WriteAllText(Path.Combine(_workingDir, "a.txt"), "a");
        File.WriteAllText(Path.Combine(_workingDir, "b.txt"), "b");

        DiffFileMenuState state = State(CommitSelection(new DiffMenuFile("M", "a.txt", null, IsTracked: true),
            new DiffMenuFile("M", "b.txt", null, IsTracked: true)));

        state.OpenWorkingFile.Should().BeFalse();
        state.EditWorkingFile.Should().BeFalse();
        state.OpenRevisionFile.Should().BeFalse();
        state.FileHistory.Should().BeFalse();
        state.Blame.Should().BeFalse();
        state.StopTracking.Should().BeFalse();
        state.SaveAs.Should().BeTrue();
        state.CopyPaths.Should().BeTrue();
    }

    [Test]
    public void For_should_not_offer_a_deleted_file_in_the_file_tree()
    {
        DiffFileMenuState state = State(CommitSelection(new DiffMenuFile("D", "gone.txt", null, IsTracked: true)));

        state.ShowInFileTree.Should().BeFalse();
        state.OpenRevisionFileEnabled.Should().BeFalse();
    }

    [Test]
    public void For_should_follow_each_files_own_revisions_as_upstreams_stash_list()
    {
        File.WriteAllText(Path.Combine(_workingDir, "staged.txt"), "s");
        File.WriteAllText(Path.Combine(_workingDir, "changed.txt"), "c");
        DiffMenuRevisions index = new(Parent, ObjectId.IndexId, [Parent]);
        DiffMenuRevisions workTree = new(ObjectId.IndexId, ObjectId.WorkTreeId, [ObjectId.IndexId]);

        DiffFileMenuState state = State(new DiffMenuSelection(Parent, ObjectId.IndexId, [Parent],
        [
            new DiffMenuFile("M", "staged.txt", null, IsTracked: true, index),
            new DiffMenuFile("M", "changed.txt", null, IsTracked: true, workTree)
        ]));

        state.Stage.Should().BeTrue("changed.txt is a working directory change");
        state.Unstage.Should().BeTrue("staged.txt is in the index");
        state.ResetToFirst.Should().Be("First: A " + Parent.ToShortString(), "upstream shows the first selected file's");
        state.DiffSecondCaption.Should().Be("Second: B " + DiffFileMenu.MultipleDescription);
        state.DiffFirstCaption.Should().Be("First: A " + DiffFileMenu.MultipleDescription);
    }

    [Test]
    public void For_should_offer_rename_for_one_tracked_file_as_upstreams_menu_does()
    {
        DiffMenuFile tracked = new("M", "a.txt", null, IsTracked: true);

        State(CommitSelection(tracked)).RenameMove.Should().BeTrue();
        State(CommitSelection(tracked, new DiffMenuFile("M", "b.txt", null, IsTracked: true))).RenameMove
            .Should().BeFalse();
        State(new DiffMenuSelection(ObjectId.IndexId, ObjectId.WorkTreeId, [],
            [new DiffMenuFile("?", "new.txt", null, IsTracked: false)])).RenameMove.Should().BeFalse();
    }

    [Test]
    public void IsValidMove_should_take_only_a_new_name_as_upstreams_move_command()
    {
        DiffFileMenu.IsValidMove(_workingDir, "a.txt", "b.txt").Should().BeTrue();
        DiffFileMenu.IsValidMove(_workingDir, "a.txt", "a.txt").Should().BeFalse();
        DiffFileMenu.IsValidMove(_workingDir, "a.txt", " ").Should().BeFalse();
    }

    [Test]
    public void ResetQuestion_should_name_the_revision_and_report_new_files()
    {
        DiffMenuSelection selection = new(ObjectId.IndexId, ObjectId.WorkTreeId, [],
            [new DiffMenuFile("?", "new.txt", null, IsTracked: false)]);

        (string revision, bool hasExisting, bool hasNew) = DiffFileMenu.ResetQuestion(selection, toFirst: true, Describe);

        revision.Should().Be("First: A Commit index");
        hasExisting.Should().BeFalse("upstream counts an untracked file as an uncommitted addition, so resetting it deletes it");
        hasNew.Should().BeTrue();
    }

    [Test]
    public void ResetQuestion_should_report_no_new_files_for_changed_files_only()
    {
        (string revision, bool hasExisting, bool hasNew) = DiffFileMenu.ResetQuestion(
            CommitSelection(new DiffMenuFile("M", "a.txt", null, IsTracked: true)), toFirst: false, Describe);

        revision.Should().Be("Second: B " + Commit.ToShortString());
        hasExisting.Should().BeTrue();
        hasNew.Should().BeFalse();
    }

    [Test]
    public void For_should_offer_a_file_of_the_file_tree_as_upstreams_file_tree_mode()
    {
        DiffFileMenuState state = State(TreeSelection(null, "src/a.txt"));

        state.Stage.Should().BeFalse();
        state.Unstage.Should().BeFalse();
        state.ResetToSecond.Should().Be("Second: B " + Commit.ToShortString());
        state.ResetToFirst.Should().BeNull("the file tree has no first revision");
        state.OpenRevisionFile.Should().BeTrue();
        state.DeleteFile.Should().BeFalse();
        state.RenameMove.Should().BeTrue();
        state.FileHistory.Should().BeTrue();
        state.Blame.Should().BeTrue();
        state.AddToIgnore.Should().BeFalse();
    }

    [Test]
    public void For_should_offer_a_selected_folder_as_upstreams_selected_folder()
    {
        DiffFileMenuState state = State(TreeSelection("src", "src/a.txt", "src/b.txt"));

        state.RenameMove.Should().BeTrue();
        state.FileHistory.Should().BeTrue();
        state.FilterFileInGrid.Should().BeTrue();
        state.Blame.Should().BeFalse("upstream blames files only");
        state.ShowInFileTree.Should().BeTrue();
    }

    [Test]
    public void For_should_offer_no_history_for_the_root_folder_of_several_files()
    {
        DiffFileMenuState state = State(TreeSelection("", "a.txt", "src/b.txt"));

        state.FileHistory.Should().BeFalse();
        state.RenameMove.Should().BeTrue();
    }

    [Test]
    public void For_should_offer_cherry_pick_changes_for_one_file_the_viewer_can_patch_as_upstream()
    {
        DiffMenuFile file = new("M", "a.txt", null, IsTracked: true);

        DiffFileMenu.For(CommitSelection(file), _workingDir, Describe, supportLinePatching: true).CherryPick
            .Should().BeTrue();
        DiffFileMenu.For(CommitSelection(file), _workingDir, Describe).CherryPick.Should().BeFalse();
        DiffFileMenu.For(CommitSelection(file, new DiffMenuFile("M", "b.txt", null, IsTracked: true)), _workingDir,
            Describe, supportLinePatching: true).CherryPick.Should().BeFalse("upstream patches one file");
        DiffFileMenu.For(new DiffMenuSelection(ObjectId.IndexId, ObjectId.WorkTreeId, [], [file]), _workingDir, Describe,
            supportLinePatching: true).CherryPick.Should().BeFalse("not the working directory's changes");
    }

    [Test]
    public void For_should_offer_the_submodule_items_for_a_working_directory_submodule_as_upstream()
    {
        Directory.CreateDirectory(Path.Combine(_workingDir, "lib"));
        DiffMenuFile submodule = new("M", "lib", null, IsTracked: true) { IsSubmodule = true };

        DiffFileMenuState workTree = State(new DiffMenuSelection(ObjectId.IndexId, ObjectId.WorkTreeId, [], [submodule]));
        DiffFileMenuState commit = State(CommitSelection(submodule));

        (workTree.Submodule, workTree.OpenSubmodule).Should().Be((true, true));
        (commit.Submodule, commit.OpenSubmodule).Should().Be((false, true), "upstream opens any submodule entry");
        State(CommitSelection(new DiffMenuFile("M", "a.txt", null, IsTracked: true))).OpenSubmodule.Should().BeFalse();
    }

    [Test]
    public void MoveTarget_should_be_the_one_file_or_the_selected_folder()
    {
        DiffFileMenu.MoveTarget(TreeSelection(null, "src/a.txt")).Should().Be(("src/a.txt", false));
        DiffFileMenu.MoveTarget(TreeSelection("src", "src/a.txt")).Should().Be(("src", true));
        DiffFileMenu.MoveTarget(TreeSelection(null, "a.txt", "b.txt")).Should().BeNull();
        DiffFileMenu.MoveTarget(TreeSelection("", "a.txt", "b.txt"))
            .Should().BeNull("a group node is upstream's root folder, whose empty name git cannot move");
    }

    [Test]
    public void The_root_folder_of_a_group_node_should_act_as_its_files()
    {
        DiffMenuSelection group = TreeSelection("", "a.txt", "src/b.txt");

        DiffFileMenu.CopiedPaths(group).Should().Equal("a.txt", "src/b.txt");
        DiffFileMenu.GridPathFilter(group).Should().Be("\"a.txt\" \"src/b.txt\"");
        DiffFileMenu.IgnorePatterns(group).Should().Equal("/a.txt", "/src/b.txt");
    }

    [Test]
    public void CopiedPaths_and_IgnorePatterns_should_take_the_selected_folder_as_upstream()
    {
        DiffMenuSelection folder = TreeSelection("src/x", "src/x/a.txt", "src/x/b.txt");
        DiffMenuSelection files = TreeSelection(null, "a.txt", "src/b.txt");

        DiffFileMenu.CopiedPaths(folder).Should().Equal("src/x");
        DiffFileMenu.CopiedPaths(files).Should().Equal("a.txt", "src/b.txt");
        DiffFileMenu.IgnorePatterns(folder).Should().Equal("/src/x/");
        DiffFileMenu.IgnorePatterns(files).Should().Equal("/a.txt", "/src/b.txt");
        DiffFileMenu.IgnorePatterns(TreeSelection("", "a.txt")).Should().Equal("/a.txt");
    }

    [Test]
    public void GridPathFilter_should_be_the_folder_or_the_files_quoted_as_upstreams()
    {
        DiffFileMenu.GridPathFilter(TreeSelection("src", "src/a.txt")).Should().Be("src");
        DiffFileMenu.GridPathFilter(TreeSelection(null, "a.txt", "my file.txt")).Should().Be("\"a.txt\" \"my file.txt\"");
    }

    [Test]
    public void FileHistoryTarget_should_be_the_file_from_its_revision_or_the_folder_from_the_checkout()
    {
        DiffFileMenu.FileHistoryTarget(TreeSelection(null, "a.txt"), "head")
            .Should().Be(("a.txt", Commit.ToString()));
        DiffFileMenu.FileHistoryTarget(TreeSelection("src", "src/a.txt"), "head").Should().Be(("src", "head"));
        DiffFileMenu.FileHistoryTarget(TreeSelection("", "a.txt"), "head").Should().BeNull();
        DiffFileMenu.FileHistoryTarget(new DiffMenuSelection(ObjectId.IndexId, ObjectId.WorkTreeId, [],
                [new DiffMenuFile("M", "a.txt", null, IsTracked: true)]), "head")
            .Should().Be(("a.txt", (string?)null));
    }

    [Test]
    public void SaveSuggestion_should_be_the_file_or_the_folder_the_files_share()
    {
        DiffMenuSelection one = CommitSelection(new DiffMenuFile("M", "src/a.txt", null, IsTracked: true));
        DiffMenuSelection two = CommitSelection(new DiffMenuFile("M", "src/x/a.txt", null, IsTracked: true),
            new DiffMenuFile("M", "src/y/b.txt", null, IsTracked: true));

        DiffFileMenu.SaveSuggestion(one, _workingDir).Should().Be(Path.Combine(_workingDir, "src", "a.txt"));
        DiffFileMenu.SaveSuggestion(two, _workingDir).Should().Be(Path.Combine(_workingDir, "src"));
    }

    private static DiffMenuSelection CommitSelection(params DiffMenuFile[] files)
        => new(Parent, Commit, [Parent], files);

    // The File tree tab's selection: the commit's files with no first revision.
    private static DiffMenuSelection TreeSelection(string? folder, params string[] files)
        => new(null, Commit, [Parent], [.. files.Select(file => new DiffMenuFile("", file, null, IsTracked: true))],
            folder, TreeFiles: true);

    private DiffFileMenuState State(DiffMenuSelection selection)
        => DiffFileMenu.For(selection, _workingDir, Describe);

    private static string Describe(ObjectId id)
        => id == ObjectId.IndexId ? "Commit index" : id == ObjectId.WorkTreeId ? "Working directory" : id.ToShortString();
}
