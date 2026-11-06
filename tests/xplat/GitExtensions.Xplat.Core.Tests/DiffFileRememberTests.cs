using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class DiffFileRememberTests
{
    private static readonly ObjectId Commit = ObjectId.Parse("1234567890123456789012345678901234567890");
    private static readonly ObjectId Parent = ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd");

    private string _workingDir = null!;

    [SetUp]
    public void SetUp()
    {
        DiffFileRemember.Forget();
        _workingDir = Path.Combine(Path.GetTempPath(), "xplat-diffremember-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workingDir);
    }

    [TearDown]
    public void TearDown()
    {
        DiffFileRemember.Forget();
        Directory.Delete(_workingDir, recursive: true);
    }

    [Test]
    public void For_should_offer_to_remember_one_file_as_upstreams_menu_does()
    {
        DiffRememberState state = DiffFileRemember.For(CommitSelection(File("M", "a.txt")), _workingDir, focused: 0);

        state.Separator.Should().BeTrue();
        state.DiffTwoSelected.Should().BeFalse();
        state.DiffWithRemembered.Should().BeNull("nothing is remembered");
        state.RememberSecond.Should().BeTrue();
        state.RememberSecondEnabled.Should().BeTrue();
        state.RememberFirst.Should().BeTrue();
        state.RememberFirstEnabled.Should().BeTrue();
    }

    [Test]
    public void For_should_not_remember_a_revision_the_file_is_not_in()
    {
        DiffRememberState added = DiffFileRemember.For(CommitSelection(File("A", "new.txt")), _workingDir, focused: 0);
        DiffRememberState deleted =
            DiffFileRemember.For(CommitSelection(File("D", "gone.txt")), _workingDir, focused: 0);

        added.RememberFirstEnabled.Should().BeFalse("an added file is not in the first revision");
        added.RememberSecondEnabled.Should().BeTrue();
        deleted.RememberSecondEnabled.Should().BeFalse("a deleted file is not in the second revision");
        deleted.RememberFirstEnabled.Should().BeTrue();
    }

    [Test]
    public void For_should_not_remember_the_working_directory_as_a_first_item()
    {
        DiffMenuSelection selection = new(ObjectId.IndexId, ObjectId.WorkTreeId, [], [File("M", "a.txt")]);

        DiffFileRemember.For(selection, _workingDir, focused: 0).RememberSecondEnabled.Should().BeFalse();
    }

    [Test]
    public void For_should_offer_two_selected_files_and_hide_the_remember_items()
    {
        DiffRememberState state = DiffFileRemember.For(CommitSelection(File("M", "a.txt"), File("M", "b.txt")),
            _workingDir, focused: 1);

        state.Separator.Should().BeTrue();
        state.DiffTwoSelected.Should().BeTrue();
        state.DiffTwoSelectedEnabled.Should().BeTrue();
        state.RememberSecond.Should().BeFalse();
        state.RememberFirst.Should().BeFalse();
    }

    [Test]
    public void For_should_offer_nothing_below_the_separator_for_three_files()
    {
        DiffRememberState state = DiffFileRemember.For(
            CommitSelection(File("M", "a.txt"), File("M", "b.txt"), File("M", "c.txt")), _workingDir, focused: 2);

        state.Separator.Should().BeFalse();
        state.DiffTwoSelected.Should().BeFalse();
        state.RememberSecond.Should().BeFalse();
    }

    [Test]
    public void For_should_offer_the_remembered_file_by_its_name_for_another_file()
    {
        DiffFileRemember.Remember(CommitSelection(File("M", "a.txt")), _workingDir, first: false);

        DiffRememberState other = DiffFileRemember.For(CommitSelection(File("M", "b.txt")), _workingDir, focused: 0);
        DiffRememberState same = DiffFileRemember.For(CommitSelection(File("M", "a.txt")), _workingDir, focused: 0);

        other.DiffWithRemembered.Should().Be("&Diff with \"a.txt\"");
        other.DiffWithRememberedEnabled.Should().BeTrue();
        same.DiffWithRemembered.Should().NotBeNull();
        same.DiffWithRememberedEnabled.Should().BeFalse("upstream does not diff the remembered file with itself");
    }

    [Test]
    public void For_should_not_offer_a_file_remembered_in_another_repository()
    {
        DiffFileRemember.Remember(CommitSelection(File("M", "a.txt")), Path.Combine(_workingDir, "other"),
            first: false);

        DiffFileRemember.For(CommitSelection(File("M", "b.txt")), _workingDir, focused: 0).DiffWithRemembered
            .Should().BeNull();
    }

    [Test]
    public void WithRememberedCommits_should_compare_the_remembered_revision_with_the_selected_file()
    {
        DiffFileRemember.Remember(CommitSelection(File("M", "a.txt")), _workingDir, first: false);

        (string? first, string? second) =
            DiffFileRemember.WithRememberedCommits(CommitSelection(File("M", "b.txt")), _workingDir);

        first.Should().Be($"{Commit}:a.txt");
        second.Should().Be($"{Commit}:b.txt");
    }

    [Test]
    public void Remember_first_should_keep_the_first_revision_under_the_old_name()
    {
        DiffFileRemember.Remember(CommitSelection(new DiffMenuFile("R", "new.txt", "old.txt", IsTracked: true)),
            _workingDir, first: true);

        (string? first, _) = DiffFileRemember.WithRememberedCommits(CommitSelection(File("M", "b.txt")), _workingDir);

        first.Should().Be($"{Parent}:old.txt");
    }

    [Test]
    public void WithRememberedCommits_should_use_the_first_revision_of_a_deleted_file()
    {
        DiffFileRemember.Remember(CommitSelection(File("M", "a.txt")), _workingDir, first: false);

        (_, string? second) =
            DiffFileRemember.WithRememberedCommits(CommitSelection(File("D", "gone.txt")), _workingDir);

        second.Should().Be($"{Parent}:gone.txt");
    }

    [Test]
    public void TwoSelectedCommits_should_take_the_file_selected_last_as_the_second()
    {
        DiffMenuSelection selection = CommitSelection(File("M", "a.txt"), File("M", "b.txt"));

        DiffFileRemember.TwoSelectedCommits(selection, _workingDir, focused: 1)
            .Should().Be(($"{Commit}:a.txt", $"{Commit}:b.txt"));
        DiffFileRemember.TwoSelectedCommits(selection, _workingDir, focused: 0)
            .Should().Be(($"{Commit}:b.txt", $"{Commit}:a.txt"));
    }

    [Test]
    public void TwoSelectedCommits_should_name_a_working_directory_file_by_its_path()
    {
        DiffMenuSelection selection = new(ObjectId.IndexId, ObjectId.WorkTreeId, [],
        [
            new DiffMenuFile("M", "a.txt", null, IsTracked: true, new DiffMenuRevisions(Parent, Commit, [Parent])),
            File("M", "b.txt")
        ]);

        DiffFileRemember.TwoSelectedCommits(selection, _workingDir, focused: 1)
            .Should().Be(($"{Commit}:a.txt", "b.txt"));
    }

    private static DiffMenuFile File(string status, string path) => new(status, path, null, IsTracked: true);

    private static DiffMenuSelection CommitSelection(params DiffMenuFile[] files) =>
        new(Parent, Commit, [Parent], files);
}
