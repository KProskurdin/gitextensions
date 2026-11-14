using AwesomeAssertions;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class BrowseAndCompareViewModelTests
{
    private static readonly ObjectId _first = ObjectId.Parse("1111111111111111111111111111111111111111");
    private static readonly ObjectId _second = ObjectId.Parse("2222222222222222222222222222222222222222");

    [Test]
    public async Task FileBrowserViewModel_should_select_a_file_given_to_open_and_open_its_blame_and_diff()
    {
        CommitRow row = new("abc", "abc", "second", "author", "2026-10-04 10:00");
        FakeCommitHistory history = new() { Tree = ["a.txt", "b.txt"], FileHistory = [row] };
        List<string> opened = [];
        FileBrowserViewModel viewModel = new(history)
        {
            OpenBlame = file => opened.Add("blame " + file),
            OpenDiff = (hash, file) => opened.Add($"diff {hash} {file}"),
        };
        viewModel.BlameCommand.CanExecute(null).Should().BeFalse();

        await viewModel.OpenAsync("/work/repo", "0123456789abcdef", selectFile: "b.txt");

        viewModel.Title.Should().Be("Files at 01234567");
        viewModel.SelectedFile.Should().Be("b.txt");
        viewModel.FileHistory.Should().Equal(row);
        viewModel.ShowHistoryDiffCommand.CanExecute(null).Should().BeFalse("no commit of the history is selected");
        viewModel.SelectedHistoryRow = row;
        viewModel.BlameCommand.Execute(null);
        viewModel.ShowHistoryDiffCommand.Execute(null);
        opened.Should().Equal("blame b.txt", "diff abc b.txt");
    }

    [Test]
    public async Task FileBrowserViewModel_should_store_the_rename_choices_and_read_the_history_again()
    {
        FakeCommitHistory history = new() { Tree = ["a.txt"] };
        InMemoryAppPreferences preferences = new() { FileHistory = new FileHistoryOptions(FollowRenames: true) };
        FileBrowserViewModel viewModel = new(history, preferences);
        await viewModel.OpenAsync("/work/repo", "abc", selectFile: "a.txt");
        history.FileHistoryRequests.Should().ContainSingle();

        viewModel.FollowRenames = false;

        preferences.FileHistory.FollowRenames.Should().BeFalse();
        history.FileHistoryRequests.Should().HaveCount(2);
    }

    [Test]
    public async Task MergeSubmoduleViewModel_should_show_the_commits_and_offer_checkout_only_with_a_callback()
    {
        IGitModule module = Substitute.For<IGitModule>();
        IGitModule submodule = Substitute.For<IGitModule>();
        ConflictData conflict = new(new ConflictedFileData(_first, "sub"), new ConflictedFileData(_second, "sub"),
            new ConflictedFileData(default, "sub"));
        module.GetConflictAsync("sub").Returns(conflict);
        submodule.GetCurrentCheckout().Returns(_second);
        module.GetSubmodule("sub").Returns(submodule);
        module.GetSubmoduleFullPath("sub").Returns("/work/repo/sub/");
        List<string> opened = [];
        MergeSubmoduleViewModel viewModel = new(new FakeDialogHost(), module, "sub", null, opened.Add);

        await viewModel.LoadAsync();
        viewModel.OpenSubmoduleCommand.Execute(null);

        (viewModel.BaseCommit, viewModel.LocalCommit, viewModel.RemoteCommit, viewModel.CurrentCommit).Should().Be(
            (_first.ToString(), _second.ToString(), FakeDialogHost.TranslatedMark + MergeSubmoduleDialog.Deleted,
                _second.ToString()));
        viewModel.CheckoutBranchCommand.CanExecute(null).Should().BeFalse("no checkout was given");
        opened.Should().Equal("/work/repo/sub/");
    }

    [Test]
    public async Task CompareToBranchViewModel_should_list_remote_branches_first_count_and_close_with_the_branch()
    {
        IGitModule module = Substitute.For<IGitModule>();
        IReadOnlyList<IGitRef> remotes = [Ref("origin/main")];
        IReadOnlyList<IGitRef> heads = [Ref("topic")];
        module.GetRefs(RefsFilter.Remotes).Returns(remotes);
        module.GetRefs(RefsFilter.Heads).Returns(heads);
        module.GetCommitCountString(_first, "topic").Returns("+1 -2");
        FakeDialogHost host = new();
        CompareToBranchViewModel viewModel = new(host, module, _first);
        int required = 0;
        viewModel.BranchRequired += (_, _) => required++;

        await viewModel.LoadAsync();
        viewModel.Branches.Should().Equal("origin/main");
        viewModel.CompareCommand.Execute(null);
        required.Should().Be(1, "Compare without a branch asks for one");

        viewModel.IsLocal = true;
        await WaitForAsync(() => viewModel.Branches.SequenceEqual(["topic"]));
        viewModel.SelectedBranch = "topic";
        await WaitForAsync(() => viewModel.Changes.Length > 0);
        viewModel.CompareCommand.Execute(null);

        viewModel.Changes.Should().Be("+1 -2");
        host.Result.Should().Be("topic");
    }

    [TestCase(GoToCommitSource.Expression, "1111111111111111111111111111111111111111")]
    [TestCase(GoToCommitSource.Tag, "2222222222222222222222222222222222222222")]
    [TestCase(GoToCommitSource.Branch, "0000000000000000000000000000000000000000")]
    public async Task GoToCommitViewModel_should_resolve_the_revision_of_the_focused_control(GoToCommitSource source,
        string expected)
    {
        IGitModule module = Substitute.For<IGitModule>();
        IReadOnlyList<IGitRef> tags = [Ref("v1", _second.ToString())];
        module.GetRefs(RefsFilter.Tags).Returns(tags);
        module.GetRefs(RefsFilter.Heads).Returns([]);
        module.RevParse("HEAD~1").Returns(_first);
        module.RevParse(_second.ToString()).Returns(_second);
        FakeDialogHost host = new();
        GoToCommitViewModel viewModel = new(host, module, () => Task.FromResult<string?>(null))
        {
            CommitExpression = " HEAD~1 ",
            TagText = "v1",
            BranchText = "nosuch",
            Source = source,
        };

        await viewModel.LoadAsync();
        await viewModel.GoCommand.ExecuteAsync(null);

        viewModel.TagNames.Should().Equal("v1");
        viewModel.LoadingText.Should().BeNull();
        host.Result.Should().Be(ObjectId.Parse(expected));
    }

    [Test]
    public async Task GoToCommitViewModel_should_take_the_clipboard_only_when_it_names_a_commit()
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.GetRefs(Arg.Any<RefsFilter>()).Returns([]);
        module.RevParse("abc123").Returns(_first);
        GoToCommitViewModel named = new(new FakeDialogHost(), module, () => Task.FromResult<string?>(" abc123 "));
        GoToCommitViewModel unnamed = new(new FakeDialogHost(), module, () => Task.FromResult<string?>("not a revision"));

        await named.LoadAsync();
        await unnamed.LoadAsync();

        named.CommitExpression.Should().Be("abc123");
        named.ExpressionFromClipboard.Should().BeTrue();
        unnamed.CommitExpression.Should().BeNull();
    }

    private static IGitRef Ref(string name, string? hash = null)
    {
        IGitRef gitRef = Substitute.For<IGitRef>();
        gitRef.Name.Returns(name);
        gitRef.LocalName.Returns(name);
        gitRef.Guid.Returns(hash ?? _first.ToString());
        return gitRef;
    }

    // The view model reads git off the UI thread and continues where it can.
    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }

        condition().Should().BeTrue();
    }
}
