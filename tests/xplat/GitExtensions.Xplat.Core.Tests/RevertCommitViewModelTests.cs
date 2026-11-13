using System.Text;
using AwesomeAssertions;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitUIPluginInterfaces;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RevertCommitViewModelTests
{
    private static readonly ObjectId _commit = ObjectId.Parse("1234567812345678123456781234567812345678");

    private FakeDialogHost _host = null!;
    private IGitModule _module = null!;
    private List<(string Arguments, bool AutoCommit)> _reverts = null!;
    private string _gitDir = null!;

    [SetUp]
    public void Setup()
    {
        _gitDir = Path.Combine(Path.GetTempPath(), "xplat-revert-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_gitDir);
        _host = new FakeDialogHost();
        _module = Substitute.For<IGitModule>();
        _module.WorkingDirGitDir.Returns(_gitDir);
        _module.CommitEncoding.Returns(Encoding.UTF8);
        _module.RevParse("HEAD").Returns(_commit);
        GitRevision revision = new(_commit) { Subject = "merge topic", Author = "Test" };
        _module.GetRevision(_commit, Arg.Any<bool>(), Arg.Any<bool>()).Returns(revision);
        _reverts = [];
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_gitDir, recursive: true);

    [Test]
    public async Task LoadAsync_of_a_merge_should_list_its_parents_and_choose_the_first()
    {
        GitRevision[] parents = [new(ObjectId.Random()) { Subject = "first parent" }, new(ObjectId.Random()) { Subject = "second" }];
        _module.IsMerge(_commit).Returns(true);
        _module.GetParentRevisions(_commit).Returns(parents);
        RevertCommitViewModel viewModel = NewViewModel();

        await viewModel.LoadAsync();

        viewModel.Summary!.Subject.Should().Be("merge topic");
        viewModel.IsMerge.Should().BeTrue();
        viewModel.Parents.Select(parent => (parent.Number, parent.Subject)).Should().Equal((1, "first parent"), (2, "second"));
        viewModel.SelectedParent.Should().Be(viewModel.Parents[0]);
    }

    [Test]
    public async Task Revert_of_a_merge_without_a_parent_should_show_upstreams_message_and_keep_the_window_open()
    {
        _module.IsMerge(_commit).Returns(true);
        _module.GetParentRevisions(_commit).Returns([new GitRevision(ObjectId.Random())]);
        RevertCommitViewModel viewModel = NewViewModel();
        await viewModel.LoadAsync();
        viewModel.SelectedParent = null;

        await viewModel.RevertCommand.ExecuteAsync(null);

        _host.Messages.Should().Equal(FakeDialogHost.TranslatedMark + CherryPickDialog.NoneParentSelected);
        _reverts.Should().BeEmpty();
        _host.IsClosed.Should().BeFalse();
    }

    [Test]
    public async Task Revert_should_run_upstreams_command_and_close_with_true()
    {
        RevertCommitViewModel viewModel = NewViewModel();
        await viewModel.LoadAsync();
        viewModel.AutoCommit = true;

        await viewModel.RevertCommand.ExecuteAsync(null);

        _reverts.Should().ContainSingle().Which.Should().Be(
            (RevertCommitDialog.Arguments(_commit, autoCommit: true, parentIndex: 0).ToString(), true));
        _host.Result.Should().Be(true);
    }

    [Test]
    public void Cancel_should_close_with_false()
    {
        RevertCommitViewModel viewModel = NewViewModel();

        viewModel.CancelCommand.Execute(null);

        _host.Result.Should().Be(false);
    }

    private RevertCommitViewModel NewViewModel()
        => new(_host, _module, "HEAD", (arguments, autoCommit) =>
        {
            _reverts.Add((arguments.ToString(), autoCommit));
            return Task.CompletedTask;
        });
}
