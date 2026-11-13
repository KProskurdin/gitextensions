using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream's <c>FormCommit.AssignCommitMessageFromTemplate</c>: git's <c>commit.template</c> as the initial message.
/// </summary>
internal sealed class CommitTemplateFileTests
{
    private const string Template = "Subject here\n\n# Explain why.\n";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        TestAppBuilder.Preferences.CloseCommitDialogAfterLastCommit = false;
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.CloseCommitDialogAfterLastCommit = true;
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_template_is_the_first_message_and_its_comments_are_left_out_of_the_commit()
    {
        File.WriteAllText(Path.Combine(_repo.Path, ".gitmessage"), Template);
        _repo.Run("config", "commit.template", ".gitmessage");
        StageChange();
        CommitWindow commit = OpenCommitWindow();
        TextBox message = Find<TextBox>(commit, "CommitMessageBox");

        WaitUntil(() => message.Text == Template);

        // As upstream: the template left as it is counts as no message.
        Click(commit, "CommitButton");

        WaitUntil(() => commit.OwnedWindows.OfType<ErrorWindow>().Any());
        commit.OwnedWindows.OfType<ErrorWindow>().Single().Close();
        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("second");

        message.Text = "Fix the thing\n\n# Explain why.\nBecause.\n";
        Click(commit, "CommitButton");

        WaitUntil(() => _repo.Run("log", "-1", "--format=%s").Trim() == "Fix the thing");
        _repo.Run("log", "-1", "--format=%B").Trim().ReplaceLineEndings("\n").Should().Be("Fix the thing\n\nBecause.");
        commit.Close();
    }

    [AvaloniaTest]
    public void A_draft_comes_before_the_template()
    {
        File.WriteAllText(Path.Combine(_repo.Path, ".gitmessage"), Template);
        _repo.Run("config", "commit.template", ".gitmessage");
        File.WriteAllText(Path.Combine(_repo.Path, ".git", "COMMITMESSAGE"), "draft");
        StageChange();
        CommitWindow commit = OpenCommitWindow();
        TextBox message = Find<TextBox>(commit, "CommitMessageBox");

        WaitUntil(() => message.Text == "draft");
        commit.Close();
    }

    [AvaloniaTest]
    public void A_missing_template_shows_upstreams_error()
    {
        _repo.Run("config", "commit.template", "missing.txt");
        StageChange();
        CommitWindow commit = OpenCommitWindow();

        WaitUntil(() => commit.OwnedWindows.OfType<ErrorWindow>().Any());

        ErrorWindow error = commit.OwnedWindows.OfType<ErrorWindow>().Single();
        error.Title.Should().Be("Template Error");
        error.FindControl<TextBlock>("MessageText")!.Text.Should()
            .StartWith($"Template not found: {Path.Combine(_repo.Path, "missing.txt")}.");
        Find<TextBox>(commit, "CommitMessageBox").Text.Should().BeNullOrEmpty();
        error.Close();
        commit.Close();
    }

    [AvaloniaTest]
    public void A_template_in_the_home_folder_is_found_as_git_finds_it()
    {
        string? home = Environment.GetEnvironmentVariable("HOME");
        string customHome = AppSettings.CustomHomeDir;
        string fakeHome = Path.Combine(Path.GetTempPath(), "xplat-home-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fakeHome);
        try
        {
            // Upstream's own setting for HOME, not the variable: on Windows upstream's EnvironmentConfiguration sets HOME again
            // from the user's profile before git runs, so a changed variable would not last.
            AppSettings.CustomHomeDir = fakeHome;
            EnvironmentConfiguration.SetEnvironmentVariables();
            File.WriteAllText(Path.Combine(fakeHome, ".gitmessage"), Template);
            _repo.Run("config", "commit.template", "~/.gitmessage");
            StageChange();
            CommitWindow commit = OpenCommitWindow();

            WaitUntil(() => Find<TextBox>(commit, "CommitMessageBox").Text == Template);
            commit.OwnedWindows.OfType<ErrorWindow>().Should().BeEmpty();
            commit.Close();
        }
        finally
        {
            AppSettings.CustomHomeDir = customHome;
            Environment.SetEnvironmentVariable("HOME", home);
            Directory.Delete(fakeHome, recursive: true);
        }
    }

    private void StageChange()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        _repo.Run("add", "a.txt");
    }

    private CommitWindow OpenCommitWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        WaitUntil(() => Find<Button>(window, "CommitDialogButton").IsEnabled);
        Click(window, "CommitDialogButton");
        WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
        return window.OwnedWindows.OfType<CommitWindow>().Single();
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        UiClicks.Raise(control, control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent);
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
                Assert.Fail("Timed out waiting for the UI");
            }

            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
    }
}
