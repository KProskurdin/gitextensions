using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Upstream's EmptyRepoControl: the revision grid's page for a repository with no commit.
internal sealed class EmptyRepositoryPanelTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private string _folder = null!;
    private bool _showArtificial;

    [SetUp]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), "xplat-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _showArtificial = TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits;
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = false;
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = _showArtificial;
        GitProcess.DeleteFolder(_folder);
    }

    [AvaloniaTest]
    public void A_repository_without_commits_shows_upstreams_panel_and_its_commit_button_opens_the_commit_window()
    {
        GitProcess.Run(_folder, "init", "-q");
        MainWindow window = OpenWindow(_folder);

        Find<StackPanel>(window, "EmptyRepoPanel").IsVisible.Should().BeTrue();
        Find<ListBox>(window, "CommitList").IsVisible.Should().BeFalse();
        Find<TextBlock>(window, "lblEmptyRepository").Text.Should()
            .Be("This repository does not yet contain any commits.");
        WaitUntil(() => Find<Button>(window, "btnOpenCommitForm").IsEffectivelyVisible);
        Find<Button>(window, "btnEditGitIgnore").IsEffectivelyVisible.Should().BeTrue();

        UiClicks.Click(Find<Button>(window, "btnOpenCommitForm"));

        WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
        window.OwnedWindows.OfType<CommitWindow>().Single().Close();
        window.Close();
    }

    [AvaloniaTest]
    public void The_working_directory_rows_stand_in_for_the_panel_when_they_are_shown()
    {
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = true;
        GitProcess.Run(_folder, "init", "-q");
        MainWindow window = OpenWindow(_folder);

        Find<StackPanel>(window, "EmptyRepoPanel").IsVisible.Should().BeFalse();
        Find<ListBox>(window, "CommitList").IsVisible.Should().BeTrue();
        window.Close();
    }

    [AvaloniaTest]
    public void A_bare_repository_without_commits_shows_the_panel_without_its_buttons()
    {
        GitProcess.Run(_folder, "init", "--bare", "-q");
        MainWindow window = OpenWindow(_folder);

        Find<StackPanel>(window, "EmptyRepoPanel").IsVisible.Should().BeTrue();
        WaitUntil(() => !Find<Button>(window, "btnOpenCommitForm").IsEffectivelyVisible);
        Find<Button>(window, "btnEditGitIgnore").IsEffectivelyVisible.Should().BeFalse();
        window.Close();
    }

    [AvaloniaTest]
    public void A_repository_with_commits_shows_the_grid()
    {
        using TestRepository repository = new();
        MainWindow window = OpenWindow(repository.Path);

        Find<StackPanel>(window, "EmptyRepoPanel").IsVisible.Should().BeFalse();
        Find<ListBox>(window, "CommitList").IsVisible.Should().BeTrue();
        window.Close();
    }

    private static MainWindow OpenWindow(string repository)
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(repository);
        WaitUntil(() => !window.IsBusy && window.OpenRepositoryPath is not null);
        return window;
    }

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"Control {name} not found");

    private static void WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition was not met in time.");
            }

            Dispatcher.UIThread.RunJobs();
        }
    }
}
