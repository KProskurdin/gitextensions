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

    private DiffFileMenuState State(DiffMenuSelection selection)
        => DiffFileMenu.For(selection, _workingDir, Describe);

    private static string Describe(ObjectId id)
        => id == ObjectId.IndexId ? "Commit index" : id == ObjectId.WorkTreeId ? "Working directory" : id.ToShortString();
}
