using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitUIPluginInterfaces;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class ArchiveViewModelTests
{
    private static readonly ObjectId _head = ObjectId.Parse("1234567812345678123456781234567812345678");
    private static readonly ObjectId _first = ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd");

    private FakeDialogHost _host = null!;
    private IGitModule _module = null!;
    private List<string> _archived = null!;
    private List<string> _asked = null!;

    [SetUp]
    public void Setup()
    {
        _host = new FakeDialogHost();
        _module = Substitute.For<IGitModule>();
        _module.WorkingDir.Returns("/work/repo");
        _module.RevParse("HEAD").Returns(_head);
        _module.RevParse("HEAD~1").Returns(_first);
        GitRevision head = new(_head) { Subject = "second", Author = "Test" };
        GitRevision first = new(_first) { Subject = "first", Author = "Other" };
        _module.GetRevision(_head, Arg.Any<bool>(), Arg.Any<bool>()).Returns(head);
        _module.GetRevision(_first, Arg.Any<bool>(), Arg.Any<bool>()).Returns(first);
        _archived = [];
        _asked = [];
    }

    [Test]
    public async Task LoadAsync_should_show_both_revisions_and_check_the_revision_filter()
    {
        ArchiveViewModel viewModel = NewViewModel(diffRevision: "HEAD~1");

        await viewModel.LoadAsync();

        viewModel.Summary!.Subject.Should().Be("second");
        viewModel.RevisionFilter.Should().BeTrue();
        viewModel.DiffHeader.Should().Be(_first.ToShortString());
        viewModel.DiffMessage.Should().Be("first");
        viewModel.DiffAuthor.Should().Be("Other");
    }

    [Test]
    public void The_filters_should_exclude_each_other()
    {
        ArchiveViewModel viewModel = NewViewModel();

        viewModel.RevisionFilter = true;
        viewModel.PathFilter = true;

        viewModel.RevisionFilter.Should().BeFalse();
        viewModel.RevisionFilter = true;
        viewModel.PathFilter.Should().BeFalse();
    }

    [Test]
    public async Task Save_with_the_revision_filter_and_no_second_revision_should_show_upstreams_message()
    {
        ArchiveViewModel viewModel = NewViewModel();
        await viewModel.LoadAsync();
        viewModel.RevisionFilter = true;

        await viewModel.SaveCommand.ExecuteAsync(null);

        _host.Messages.Should().Equal(FakeDialogHost.TranslatedMark + ArchiveDialog.NoRevisionSelected);
        _asked.Should().BeEmpty();
    }

    [Test]
    public async Task Save_should_ask_for_the_file_archive_the_paths_and_close()
    {
        ArchiveViewModel viewModel = NewViewModel();
        await viewModel.LoadAsync();
        viewModel.PathFilter = true;
        viewModel.Paths = "a.txt";
        viewModel.IsZip = false;

        await viewModel.SaveCommand.ExecuteAsync(null);

        _asked.Should().ContainSingle().Which.Should().EndWith("|tar");
        _archived.Should().ContainSingle().Which.Should().Contain("out.tar").And.Contain("a.txt");
        _host.IsClosed.Should().BeTrue();
    }

    [Test]
    public async Task Save_cancelled_in_the_file_picker_should_keep_the_window_open()
    {
        ArchiveViewModel viewModel = NewViewModel();
        await viewModel.LoadAsync();
        viewModel.PickOutput = (_, _) => Task.FromResult<string?>(null);

        await viewModel.SaveCommand.ExecuteAsync(null);

        _archived.Should().BeEmpty();
        _host.IsClosed.Should().BeFalse();
    }

    private ArchiveViewModel NewViewModel(string? diffRevision = null)
        => new(_host, _module, "HEAD", diffRevision, path: null,
            arguments =>
            {
                _archived.Add(arguments);
                return Task.CompletedTask;
            },
            _ => Task.FromResult<string?>(null),
            (suggestion, extension) =>
            {
                _asked.Add($"{suggestion}|{extension}");
                return Task.FromResult<string?>($"out.{extension}");
            });
}
