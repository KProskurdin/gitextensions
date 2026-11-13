using System.ComponentModel;
using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CreateTagViewModelTests
{
    private static readonly ObjectId _head = ObjectId.Parse("1234567812345678123456781234567812345678");
    private static readonly ObjectId _given = ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd");

    private FakeDialogHost _host = null!;
    private IGitModule _module = null!;
    private List<string> _runs = null!;
    private List<(string Remote, string Tag)> _pushes = null!;
    private bool _runSucceeds;

    [SetUp]
    public void Setup()
    {
        _host = new FakeDialogHost();
        _module = Substitute.For<IGitModule>();
        _module.WorkingDir.Returns("/work/repo");
        _module.GetCurrentCheckout().Returns(_head);
        _module.RevParse("v0.9").Returns(_given);
        _module.GetCurrentRemote().Returns("upstream");
        _runs = [];
        _pushes = [];
        _runSucceeds = true;
    }

    [Test]
    public async Task LoadAsync_should_take_the_current_checkout_and_name_the_remote_in_the_push_box()
    {
        CreateTagViewModel viewModel = NewViewModel(revision: null);

        await viewModel.LoadAsync();

        viewModel.Revision.Should().Be(_head);
        viewModel.PushTagCaption.Should().Be(FakeDialogHost.TranslatedMark + "Push tag to 'upstream'");
        viewModel.Loaded.IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task LoadAsync_should_resolve_the_given_commit()
    {
        CreateTagViewModel viewModel = NewViewModel(revision: "v0.9");

        await viewModel.LoadAsync();

        viewModel.Revision.Should().Be(_given);
    }

    [Test]
    public void The_kinds_should_be_upstreams_translated_texts_and_drive_the_key_and_message_boxes()
    {
        CreateTagViewModel viewModel = NewViewModel();
        List<string?> changed = [];
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.Operations.Should().Equal(
            CreateTagDialog.Operations.Select(operation => FakeDialogHost.TranslatedMark + operation.Text));
        viewModel.UsesKeyId.Should().BeFalse();
        viewModel.UsesMessage.Should().BeFalse("a lightweight tag has no message");

        viewModel.SelectedOperationIndex = 3;

        viewModel.UsesKeyId.Should().BeTrue();
        viewModel.UsesMessage.Should().BeTrue();
        changed.Should().Contain([nameof(CreateTagViewModel.UsesKeyId), nameof(CreateTagViewModel.UsesMessage)]);
    }

    [Test]
    public async Task Create_without_a_commit_should_show_upstreams_message_and_keep_the_window_open()
    {
        _module.GetCurrentCheckout().Returns(default(ObjectId));
        CreateTagViewModel viewModel = NewViewModel();
        await viewModel.LoadAsync();

        await viewModel.CreateCommand.ExecuteAsync(null);

        _host.Messages.Should().Equal(FakeDialogHost.TranslatedMark + CreateTagDialog.NoRevisionSelected);
        _host.IsClosed.Should().BeFalse();
        _runs.Should().BeEmpty();
    }

    [Test]
    public async Task Create_should_tag_close_with_true_and_push_when_asked()
    {
        CreateTagViewModel viewModel = NewViewModel();
        await viewModel.LoadAsync();
        viewModel.TagName = "v1.0";
        viewModel.PushTag = true;

        await viewModel.CreateCommand.ExecuteAsync(null);

        _runs.Should().ContainSingle().Which.Should().Contain("tag").And.Contain("v1.0");
        _host.Result.Should().Be(true);
        _pushes.Should().Equal(("upstream", "v1.0"));
    }

    [Test]
    public async Task Create_should_keep_the_window_open_and_not_push_when_git_fails()
    {
        _runSucceeds = false;
        CreateTagViewModel viewModel = NewViewModel();
        await viewModel.LoadAsync();
        viewModel.TagName = "v1.0";
        viewModel.PushTag = true;

        await viewModel.CreateCommand.ExecuteAsync(null);

        _host.IsClosed.Should().BeFalse();
        _pushes.Should().BeEmpty();
    }

    private CreateTagViewModel NewViewModel(string? revision = null)
        => new(_host, _module, revision,
            arguments =>
            {
                _runs.Add(arguments);
                return Task.FromResult(_runSucceeds);
            },
            (remote, tag) =>
            {
                _pushes.Add((remote, tag));
                return Task.FromResult(true);
            });
}
