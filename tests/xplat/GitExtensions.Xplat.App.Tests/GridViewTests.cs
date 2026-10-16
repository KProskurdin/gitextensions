using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class GridViewTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Reset();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_columns_menu_hides_and_shows_a_column_and_keeps_the_choice()
    {
        MainWindow window = OpenWindow();

        Click(window, "ShowAuthorNameColumnMenuItem");

        window.Resources["ShowAuthorNameColumn"].Should().Be(false);
        TestAppBuilder.Preferences.GridColumns.AuthorName.Should().BeFalse();
        Find<MenuItem>(window, "ShowAuthorNameColumnMenuItem").IsChecked.Should().BeFalse();

        Click(window, "ShowAuthorNameColumnMenuItem");

        window.Resources["ShowAuthorNameColumn"].Should().Be(true);
    }

    [AvaloniaTest]
    public void Show_tags_and_show_commit_body_change_the_rows()
    {
        _repo.Run("tag", "v1");
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        CommitListItem Head() => Rows(commits).First(item => item.Row.Subject == "second");
        WaitUntil(() => Head().GridLabels.Any(label => label.Kind == RefKind.Tag));
        Head().BodyText.Should().Be(" body line");

        Click(window, "ShowTagsMenuItem");
        Click(window, "ShowCommitBodyMenuItem");

        WaitUntil(() => Head().GridLabels.All(label => label.Kind != RefKind.Tag));
        Head().BodyText.Should().BeEmpty();
        TestAppBuilder.Preferences.GridViewOptions.Should().Be(new GridViewOptions(ShowTags: false, ShowCommitBody: false));
    }

    [AvaloniaTest]
    public void The_rows_of_the_selected_commits_author_are_tinted()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "o.txt"), "x");
        _repo.Run("add", "o.txt");
        _repo.Run("-c", "user.name=Other", "-c", "user.email=other@example.com", "commit", "-q", "-m", "other's work");
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");

        // Without a selection, the user's own commits.
        WaitUntil(() => Tinted(commits).SequenceEqual(["second", "first"]));

        commits.SelectedItem = Rows(commits).First(item => item.Row.Subject == "other's work");

        WaitUntil(() => Tinted(commits).SequenceEqual(["other's work"]));
    }

    [AvaloniaTest]
    public void A_remotes_color_reaches_the_grid_with_the_labels_that_take_it()
    {
        string head = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("remote", "add", "origin", Path.GetTempPath());
        _repo.Run("config", "remote.origin.color", "#FF8000");
        _repo.Run("update-ref", $"refs/remotes/origin/{head}", "HEAD");
        _repo.Run("config", $"branch.{head}.remote", "origin");
        _repo.Run("config", $"branch.{head}.merge", $"refs/heads/{head}");
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        CommitListItem Head() => Rows(commits).First(item => item.Row.Subject == "second");

        WaitUntil(() => Head().GridLabels.Any(label => label.Nestled is not null)
                        && commits.GetValue(RefLabelView.RemoteColorsProperty) is { Count: > 0 });

        Head().GridLabels.Single(label => label.Nestled is not null).Nestled!.Remote.Should().Be("origin");
        commits.GetValue(RefLabelView.RemoteColorsProperty)!["origin"].ToArgb().Should()
            .Be(System.Drawing.Color.FromArgb(0xFF, 0x80, 0x00).ToArgb());
    }

    private static IReadOnlyList<CommitListItem> Rows(ListBox commits)
        => commits.ItemsSource as IReadOnlyList<CommitListItem> ?? [];

    private static IEnumerable<string> Tinted(ListBox commits)
        => commits.GetRealizedContainers()
            .Where(container => container.Classes.Contains("authored"))
            .Select(container => ((CommitListItem)container.DataContext!).Row.Subject);

    private static void Reset()
    {
        TestAppBuilder.Preferences.GridViewOptions = new GridViewOptions();
        TestAppBuilder.Preferences.GridColumns = new GridColumns();
        TestAppBuilder.Preferences.HighlightAuthoredRevisions = true;
        TestAppBuilder.Preferences.RevisionSortOrder = RevisionSortOrder.GitDefault;
    }

    private static MainWindow NewWindow() =>
        new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));

    private MainWindow OpenWindow()
    {
        MainWindow window = NewWindow();
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        return window;
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        RoutedEvent click = control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent;
        control.RaiseEvent(new RoutedEventArgs(click));
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
