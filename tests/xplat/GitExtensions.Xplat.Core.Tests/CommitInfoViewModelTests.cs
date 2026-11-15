using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CommitInfoViewModelTests
{
    private const string RepositoryPath = "/work/repo";
    private const string Hash = "1234567890abcdef1234567890abcdef12345678";

    private static readonly CommitDetails _details =
        new(Hash, "Ann Author <ann@example.com>", "", "", "", "subject\n\nbody");

    private InMemoryAppPreferences _preferences = null!;
    private List<CommitInfoRequest> _requests = null!;
    private Exception? _readFailure;

    [SetUp]
    public void Setup()
    {
        _preferences = new InMemoryAppPreferences { ShowAuthorAvatarInCommitInfo = true };
        _requests = [];
        _readFailure = null;
    }

    [Test]
    public async Task Show_should_show_the_message_the_avatar_and_read_the_information_after_it()
    {
        CommitInfoViewModel viewModel = NewViewModel();

        viewModel.Show(_details, error: null, childHashes: []);
        await WaitUntilAsync(() => viewModel.RevisionInfoLines.Count > 0);

        CommitInfoText.PlainText(viewModel.MessageLines).Should().Contain("subject").And.Contain("body");
        viewModel.IsAvatarVisible.Should().BeTrue();
        viewModel.AvatarName.Should().Be("Ann Author");
        viewModel.AvatarEmail.Should().Be("ann@example.com");
        _requests.Should().ContainSingle().Which.Should().Match<CommitInfoRequest>(request =>
            request.RepositoryPath == RepositoryPath && request.Hash == Hash && !request.ShowAllBranches);
        CommitInfoText.PlainText(viewModel.RevisionInfoLines).Should().Be("Contained in branches:");
    }

    [Test]
    public void Show_without_a_commit_should_show_the_error_in_place_of_the_message()
    {
        CommitInfoViewModel viewModel = NewViewModel();

        viewModel.Show(details: null, "bad object", childHashes: []);

        CommitInfoText.PlainText(viewModel.MessageLines).Should().Be("bad object");
        viewModel.HeaderRows.Should().BeEmpty();
        viewModel.IsAvatarVisible.Should().BeFalse();
        _requests.Should().BeEmpty();
    }

    [Test]
    public async Task A_menu_option_should_be_stored_and_read_the_information_again()
    {
        CommitInfoViewModel viewModel = await ShownAsync();

        viewModel.ToggleOptionCommand.Execute(CommitInfoOption.Tags);
        await WaitUntilAsync(() => _requests.Count == 2);

        _preferences.CommitInfoOptions.Tags.Should().BeFalse();
        viewModel.Tags.Should().BeFalse();
        _requests[^1].Options.Tags.Should().BeFalse();
    }

    [Test]
    public async Task Links_should_go_to_revisions_list_all_branches_or_open_in_the_browser()
    {
        CommitInfoViewModel viewModel = await ShownAsync();
        List<string> revisions = [];
        List<Uri> opened = [];
        viewModel.RevisionLinkClicked += (_, revision) => revisions.Add(revision);
        viewModel.OpenUri = address =>
        {
            opened.Add(address);
            return Task.CompletedTask;
        };

        viewModel.OpenLinkCommand.Execute("gitext://gotobranch/main");
        viewModel.OpenLinkCommand.Execute("gitext://showall/branches");
        viewModel.OpenLinkCommand.Execute("https://example.com/issue/1");
        await WaitUntilAsync(() => _requests.Count == 2);

        revisions.Should().Equal("main");
        _requests[^1].ShowAllBranches.Should().BeTrue();
        _requests[^1].ShowAllTags.Should().BeFalse();
        opened.Should().Equal(new Uri("https://example.com/issue/1"));
    }

    [Test]
    public async Task Copy_should_put_the_menus_link_or_the_commits_text_on_the_clipboard()
    {
        CommitInfoViewModel viewModel = await ShownAsync();
        List<string> copied = [];
        viewModel.SetClipboard = text =>
        {
            copied.Add(text);
            return Task.CompletedTask;
        };

        viewModel.IsCopyLinkVisible.Should().BeFalse();
        viewModel.MenuLink = "https://example.com/a&b";
        await viewModel.CopyLinkCommand.ExecuteAsync(null);
        await viewModel.CopyCommitInfoCommand.ExecuteAsync(null);

        viewModel.IsCopyLinkVisible.Should().BeTrue();
        viewModel.CopyLinkHeader.Should().Be("Copy &link (https://example.com/a&b)");
        copied[0].Should().Be("https://example.com/a&b");
        copied[1].Should().EndWith($"subject{Environment.NewLine}{Environment.NewLine}body");
    }

    [Test]
    public async Task A_failed_read_should_show_its_error_after_the_message()
    {
        _readFailure = new InvalidOperationException("git failed");
        CommitInfoViewModel viewModel = NewViewModel();

        viewModel.Show(_details, error: null, childHashes: []);
        await WaitUntilAsync(() => viewModel.RevisionInfoLines.Count > 0);

        CommitInfoText.PlainText(viewModel.RevisionInfoLines).Should().Be("git failed");
    }

    private async Task<CommitInfoViewModel> ShownAsync()
    {
        CommitInfoViewModel viewModel = NewViewModel();
        viewModel.Show(_details, error: null, childHashes: []);
        await WaitUntilAsync(() => _requests.Count == 1 && viewModel.RevisionInfoLines.Count > 0);
        return viewModel;
    }

    private CommitInfoViewModel NewViewModel()
        => new(_preferences, (request, _) =>
        {
            lock (_requests)
            {
                _requests.Add(request);
            }

            return _readFailure is { } failure
                ? Task.FromException<IReadOnlyList<IReadOnlyList<InfoSpan>>>(failure)
                : Task.FromResult<IReadOnlyList<IReadOnlyList<InfoSpan>>>([[new InfoSpan("Contained in branches:")]]);
        })
        {
            RepositoryPath = RepositoryPath,
        };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out");
            }

            await Task.Delay(10);
        }
    }
}
