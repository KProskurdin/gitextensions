using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// Upstream's FormOpenDirectory.
internal sealed class OpenDirectoryViewModelTests
{
    private static readonly char _separator = Path.DirectorySeparatorChar;

    private readonly HashSet<string> _repositories = [];
    private FakeDialogHost _host = null!;
    private string _folder = null!;

    [SetUp]
    public void Setup()
    {
        _repositories.Clear();
        _host = new FakeDialogHost();
        _folder = Directory.CreateTempSubdirectory("xplat-open-").FullName;
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [Test]
    public void The_texts_are_upstreams()
    {
        string form = File.ReadAllText(UpstreamFile("FormOpenDirectory.cs"));
        string designer = File.ReadAllText(UpstreamFile("FormOpenDirectory.Designer.cs"));

        form.Should().Contain($"new(\"{OpenDirectoryViewModel.WarningOpenFailed}\")");
        designer.Should().Contain("Text = \"Open local repository\"").And.Contain("label1.Text = \"&Directory:\"")
            .And.Contain("Load.Text = \"Open\"").And.Contain("folderBrowserButton.Text = \"&Browse...\"")
            .And.Contain("\"Go to parent directory...\"");
    }

    [Test]
    public void ListDirectories_should_list_the_clone_folder_the_open_repositorys_parent_and_the_recent_ones()
    {
        string clone = Path.Combine(_folder, "clones");
        string open = Path.Combine(_folder, "work", "repo");
        string recent = Path.Combine(_folder, "recent") + _separator;

        OpenDirectoryViewModel.ListDirectories(clone, open, [recent, recent], "ignored", "ignored too").Should().Equal(
            clone + _separator, Path.Combine(_folder, "work") + _separator, recent);
    }

    [Test]
    public void ListDirectories_should_offer_the_last_repository_and_home_when_there_is_nothing_else()
    {
        OpenDirectoryViewModel.ListDirectories("", null, [], Path.Combine(_folder, "last"), _folder).Should().Equal(
            Path.Combine(_folder, "last") + _separator, _folder + _separator);
        OpenDirectoryViewModel.ListDirectories(null, null, [], null, null).Should().BeEmpty();
    }

    [Test]
    public void The_folder_should_start_on_the_lists_first_entry()
        => New(["first", "second"]).Directory.Should().Be("first");

    [Test]
    public void GoUp_should_show_the_parent_folder_and_only_for_an_existing_folder()
    {
        string child = Directory.CreateDirectory(Path.Combine(_folder, "child")).FullName;
        OpenDirectoryViewModel viewModel = New([child]);
        viewModel.CanGoUp.Should().BeTrue();

        viewModel.GoUpCommand.Execute(null);

        viewModel.Directory.Should().Be(_folder + _separator);
        viewModel.Directory = Path.Combine(_folder, "missing");
        viewModel.CanGoUp.Should().BeFalse();
        viewModel.GoUpCommand.CanExecute(null).Should().BeFalse();
        viewModel.Directory = "\0";
        viewModel.CanGoUp.Should().BeFalse("what is typed is no folder path");
    }

    [Test]
    public async Task Open_should_choose_a_repository_and_close()
    {
        string repository = Directory.CreateDirectory(Path.Combine(_folder, "repo")).FullName;
        _repositories.Add(repository + _separator);
        OpenDirectoryViewModel viewModel = New([]);
        viewModel.Directory = $"  {repository}{_separator} ";

        await viewModel.OpenCommand.ExecuteAsync(null);

        viewModel.ChosenRepository.Should().Be(repository);
        _host.IsClosed.Should().BeTrue();
        _host.Result.Should().Be(true);
        _host.Messages.Should().BeEmpty();
    }

    [Test]
    public async Task Open_should_say_a_folder_that_is_no_repository_is_not_valid_and_stay_open()
    {
        OpenDirectoryViewModel viewModel = New([_folder, Path.Combine(_folder, "missing")]);

        await viewModel.OpenCommand.ExecuteAsync(null);
        viewModel.Directory = Path.Combine(_folder, "missing");
        await viewModel.OpenCommand.ExecuteAsync(null);

        _host.Messages.Should()
            .Equal(Enumerable.Repeat(FakeDialogHost.TranslatedMark + OpenDirectoryViewModel.WarningOpenFailed, 2));
        _host.IsClosed.Should().BeFalse();
        viewModel.ChosenRepository.Should().BeNull();
    }

    [Test]
    public async Task Browse_should_start_at_the_folder_and_open_the_one_chosen()
    {
        string repository = Directory.CreateDirectory(Path.Combine(_folder, "repo")).FullName;
        _repositories.Add(repository);
        string? start = null;
        OpenDirectoryViewModel viewModel = New([_folder]);
        viewModel.PickFolder = folder =>
        {
            start = folder;
            return Task.FromResult<string?>(repository);
        };

        await viewModel.BrowseCommand.ExecuteAsync(null);

        start.Should().Be(_folder);
        viewModel.ChosenRepository.Should().Be(repository);
        _host.IsClosed.Should().BeTrue();
    }

    [Test]
    public async Task Browse_should_do_nothing_when_no_folder_was_chosen()
    {
        OpenDirectoryViewModel viewModel = New([_folder]);
        viewModel.PickFolder = _ => Task.FromResult<string?>(null);

        await viewModel.BrowseCommand.ExecuteAsync(null);

        viewModel.Directory.Should().Be(_folder);
        _host.Messages.Should().BeEmpty();
        _host.IsClosed.Should().BeFalse();
    }

    private OpenDirectoryViewModel New(IReadOnlyList<string> directories)
        => new(_host, directories, _repositories.Contains);

    private static string UpstreamFile(string file, [CallerFilePath] string testFile = "")
        => Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", "..", "src", "app", "GitUI", "CommandsDialogs",
            "BrowseDialog", file);
}
