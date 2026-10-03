using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Xplat.App;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class MainWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
    }

    [TearDown]
    public void TearDown()
    {
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void Shows_install_message_and_disables_open_when_git_is_missing()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.NotFound, Command: null, Version: null));

        Find<TextBlock>(window, "GitProblemText").Text.Should().Contain("git was not found");
        Find<Button>(window, "OpenButton").IsEnabled.Should().BeFalse();
    }

    [AvaloniaTest]
    public void Shows_version_message_when_git_is_too_old()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.TooOld, "/usr/bin/git",
            new GitVersion("git version 2.20.1")));

        Find<TextBlock>(window, "GitProblemText").Text.Should().Contain("2.20.1");
        Find<Button>(window, "OpenButton").IsEnabled.Should().BeTrue();
    }

    [AvaloniaTest]
    public void Open_lists_commits_and_titles_the_window_after_the_repository()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));

        Open(window, _repo.Path);

        Find<TextBlock>(window, "StatusText").Text.Should().Be("2 commits");
        Find<ListBox>(window, "CommitList").ItemCount.Should().Be(2);
        window.Title.Should().Be($"{Path.GetFileName(_repo.Path)} - Git Extensions");
    }

    [AvaloniaTest]
    public void Opening_a_folder_that_is_not_a_repository_shows_an_error_dialog()
    {
        string folder = Path.Combine(Path.GetTempPath(), "xplat-not-a-repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
            window.Show();

            Open(window, folder);
            WaitUntil(() => window.OwnedWindows.Count == 1);

            ErrorWindow dialog = (ErrorWindow)window.OwnedWindows[0];
            Find<TextBlock>(dialog, "MessageText").Text.Should().Contain("Not a git repository");
            dialog.Close();
            window.Close();
        }
        finally
        {
            Directory.Delete(folder);
        }
    }

    [AvaloniaTest]
    public void Open_shows_the_current_branch_and_the_changed_files()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));

        Open(window, _repo.Path);
        ListBox changes = Find<ListBox>(window, "ChangeList");
        WaitUntil(() => changes.ItemCount == 1);

        string current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        Find<TextBlock>(window, "BranchText").Text.Should().Be($"Branch: {current}");
    }

    [AvaloniaTest]
    public void Staging_and_committing_from_the_window_adds_a_commit()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        Open(window, _repo.Path);
        ListBox changes = Find<ListBox>(window, "ChangeList");
        WaitUntil(() => changes.ItemCount == 1);

        changes.SelectedIndex = 0;
        Click(window, "StageButton");
        WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Staged");

        Find<TextBox>(window, "CommitMessageBox").Text = "add new";
        WaitUntil(() => Find<Button>(window, "CommitButton").IsEnabled);
        Click(window, "CommitButton");
        WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Committed");

        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("add new");
        Find<TextBox>(window, "CommitMessageBox").Text.Should().BeEmpty();
    }

    [AvaloniaTest]
    public void Selecting_a_commit_shows_its_details()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        Open(window, _repo.Path);
        ListBox list = Find<ListBox>(window, "CommitList");

        list.SelectedIndex = 0;
        WaitUntil(() => Find<TextBlock>(window, "DetailMessage").Text == "second\n\nbody line");

        Find<TextBlock>(window, "DetailHash").Text.Should().Be(_repo.Run("rev-parse", "HEAD").Trim());
        Find<TextBlock>(window, "DetailAuthor").Text.Should().Be("Test <test@example.com>");
        Find<TextBlock>(window, "DetailParents").Text.Should().StartWith("Parents: ");
    }

    [AvaloniaTest]
    public void Selecting_the_root_commit_shows_no_parents()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        Open(window, _repo.Path);
        ListBox list = Find<ListBox>(window, "CommitList");

        list.SelectedIndex = 1;
        WaitUntil(() => Find<TextBlock>(window, "DetailParents").Text == "No parents");

        Find<TextBlock>(window, "DetailMessage").Text.Should().Be("first");
    }

    private static void Open(MainWindow window, string path)
    {
        Find<TextBox>(window, "PathBox").Text = path;
        Find<Button>(window, "OpenButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => Find<Button>(window, "OpenButton").IsEnabled);
    }

    private static void Click(Window window, string buttonName)
        => Find<Button>(window, buttonName).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"Control {name} not found");

    // Background work posts its results back to the UI thread; run those jobs until the condition holds.
    private static void WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the UI");
            }

            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
    }
}
