using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using GitUIPluginInterfaces;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CherryPickViewModelTests
{
    private static readonly ObjectId _commit = ObjectId.Parse("1234567812345678123456781234567812345678");
    private static readonly ObjectId _other = ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd");

    private FakeDialogHost _host = null!;
    private IGitModule _module = null!;
    private InMemoryAppPreferences _preferences = null!;
    private List<(string Hash, string Arguments)> _picks = null!;
    private string? _chosen;

    [SetUp]
    public void Setup()
    {
        _host = new FakeDialogHost();
        _module = Substitute.For<IGitModule>();
        _module.RevParse(_commit.ToString()).Returns(_commit);
        _module.RevParse(_other.ToString()).Returns(_other);
        GitRevision commit = new(_commit) { Subject = "fix", Author = "Test" };
        GitRevision other = new(_other) { Subject = "other fix", Author = "Test" };
        _module.GetRevision(_commit, Arg.Any<bool>(), Arg.Any<bool>()).Returns(commit);
        _module.GetRevision(_other, Arg.Any<bool>(), Arg.Any<bool>()).Returns(other);
        _preferences = new InMemoryAppPreferences
        {
            CommitAutomaticallyAfterCherryPick = true, AddCommitReferenceToCherryPick = false,
        };
        _picks = [];
        _chosen = null;
    }

    [Test]
    public async Task The_options_should_start_from_upstreams_settings_and_be_kept_after_a_pick()
    {
        CherryPickViewModel viewModel = NewViewModel();
        await viewModel.LoadAsync();
        viewModel.AutoCommit.Should().BeTrue();
        viewModel.AddReference.Should().BeFalse();
        viewModel.AddReference = true;

        await viewModel.PickCommand.ExecuteAsync(null);

        _picks.Should().ContainSingle().Which.Hash.Should().Be(_commit.ToString());
        _preferences.AddCommitReferenceToCherryPick.Should().BeTrue();
        _host.Result.Should().Be(true);
    }

    [Test]
    public async Task Choosing_another_revision_should_show_that_commit()
    {
        CherryPickViewModel viewModel = NewViewModel();
        await viewModel.LoadAsync();
        _chosen = _other.ToString();

        await viewModel.ChooseRevisionCommand.ExecuteAsync(null);

        viewModel.Summary!.Subject.Should().Be("other fix");
    }

    [Test]
    public async Task Choosing_nothing_should_keep_the_commit()
    {
        CherryPickViewModel viewModel = NewViewModel();
        await viewModel.LoadAsync();

        await viewModel.ChooseRevisionCommand.ExecuteAsync(null);

        viewModel.Summary!.Subject.Should().Be("fix");
    }

    [Test]
    public async Task A_merge_should_be_picked_from_the_chosen_parent()
    {
        _module.IsMerge(_commit).Returns(true);
        _module.GetParentRevisions(_commit).Returns([new GitRevision(_other) { Subject = "first" }, new GitRevision(_other)]);
        CherryPickViewModel viewModel = NewViewModel();
        await viewModel.LoadAsync();
        viewModel.SelectedParent = viewModel.Parents[1];

        await viewModel.PickCommand.ExecuteAsync(null);

        _picks.Should().ContainSingle().Which.Arguments.Should().Contain("-m 2");
    }

    private CherryPickViewModel NewViewModel()
        => new(_host, _module, _commit.ToString(), _preferences,
            (hash, arguments) =>
            {
                _picks.Add((hash, arguments.ToString()));
                return Task.CompletedTask;
            },
            _ => Task.FromResult(_chosen));
}
