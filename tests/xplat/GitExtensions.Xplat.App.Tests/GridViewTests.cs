using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
    public void The_date_and_SHA1_columns_start_at_upstreams_widths()
    {
        MainWindow window = OpenWindow();

        window.Resources["DateColumnWidth"].Should().Be(130.0);
        window.Resources["ObjectIdColumnWidth"].Should().BeOfType<double>().Which.Should().BePositive();
    }

    [AvaloniaTest]
    public void The_date_column_fits_an_absolute_date()
    {
        TestAppBuilder.Preferences.RelativeDate = false;

        MainWindow window = OpenWindow();

        window.Resources["DateColumnWidth"].Should().BeOfType<double>().Which.Should().BePositive().And.NotBe(130.0);
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

        // Without a selection (the grid opens on HEAD, so it is cleared), the user's own commits.
        WaitUntil(() => commits.SelectedItem is not null);
        commits.SelectedItems!.Clear();
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

    [AvaloniaTest]
    public void A_hovered_label_shows_upstreams_tooltip_of_its_branch_and_the_one_nestled_against_it()
    {
        string head = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("remote", "add", "origin", Path.GetTempPath());
        _repo.Run("update-ref", $"refs/remotes/origin/{head}", "HEAD");
        _repo.Run("config", $"branch.{head}.remote", "origin");
        _repo.Run("config", $"branch.{head}.merge", $"refs/heads/{head}");
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => commits.GetVisualDescendants().OfType<RefLabelView>()
            .Any(view => view.Label?.Nestled is not null && view.Bounds.Width > 0));
        RefLabelView label = commits.GetVisualDescendants().OfType<RefLabelView>().First(view => view.Label?.Nestled is not null);

        window.MouseMove(label.TranslatePoint(new Point(3, label.Bounds.Height / 2), window)!.Value);
        Dispatcher.UIThread.RunJobs();

        string branch = (ToolTip.GetTip(label) as string)!.ReplaceLineEndings("\n");
        branch.Should().StartWith($"[{head}]   ").And.Contain($"\nis tracking [origin/{head}]\n\nsecond");

        // The nestled remote label ends before the margin after it.
        window.MouseMove(label.TranslatePoint(new Point(label.Bounds.Width - 10, label.Bounds.Height / 2), window)!.Value);
        Dispatcher.UIThread.RunJobs();

        (ToolTip.GetTip(label) as string)!.ReplaceLineEndings("\n").Should().StartWith($"[origin/{head}]\nis tracked by [{head}]   ");

        window.MouseMove(new Point(1, 1));
        Dispatcher.UIThread.RunJobs();

        ToolTip.GetTip(label).Should().BeNull("the pointer left the label");
    }

    [AvaloniaTest]
    public void The_colors_pages_revision_graph_group_saves_upstreams_settings_and_grays_the_non_relatives()
    {
        string head = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("checkout", "-q", "-b", "side", "HEAD~1");
        File.WriteAllText(Path.Combine(_repo.Path, "side.txt"), "side");
        _repo.Run("add", "side.txt");
        _repo.Run("commit", "-q", "-m", "side work");
        _repo.Run("checkout", "-q", head);
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => Rows(commits).Any(item => item.Row.Subject == "side work"));
        Rows(commits).Should().NotContain(item => item.IsNonRelativeText, "upstream's setting is off by default");

        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);
        string[] boxes =
        [
            "MulticolorBranches", "chkDrawAlternateBackColor", "DrawNonRelativesGray", "DrawNonRelativesTextGray",
            "chkHighlightAuthored", "chkFillRefLabels",
        ];
        boxes.Select(name => settings.FindControl<CheckBox>(name)!.IsChecked).Should()
            .Equal(true, true, true, false, true, false);

        settings.FindControl<CheckBox>("MulticolorBranches")!.IsChecked = false;
        settings.FindControl<CheckBox>("DrawNonRelativesGray")!.IsChecked = false;
        settings.FindControl<CheckBox>("DrawNonRelativesTextGray")!.IsChecked = true;
        settings.FindControl<CheckBox>("chkFillRefLabels")!.IsChecked = true;
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        TestAppBuilder.Preferences.MulticolorBranches.Should().BeFalse();
        TestAppBuilder.Preferences.RevisionGraphDrawNonRelativesGray.Should().BeFalse();
        TestAppBuilder.Preferences.RevisionGraphDrawNonRelativesTextGray.Should().BeTrue();
        TestAppBuilder.Preferences.FillRefLabels.Should().BeTrue();

        // The window applies them after the settings window has closed.
        WaitUntil(() => window.Resources["FillRefLabels"] is true);
        Find<MenuItem>(window, "DrawNonRelativesGrayMenuItem").IsChecked.Should().BeFalse();
        WaitUntil(() => Rows(commits).Single(item => item.Row.Subject == "side work").IsNonRelativeText);
        Rows(commits).Where(item => item.IsNonRelativeText).Select(item => item.Row.Subject).Should().Equal("side work");
    }

    [AvaloniaTest]
    public void The_remote_branch_the_checked_out_branch_tracks_is_its_merge_source()
    {
        string head = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("remote", "add", "origin", Path.GetTempPath());
        _repo.Run("update-ref", $"refs/remotes/origin/{head}", "HEAD~1");
        _repo.Run("update-ref", "refs/remotes/origin/other", "HEAD~1");
        _repo.Run("config", $"branch.{head}.remote", "origin");
        _repo.Run("config", $"branch.{head}.merge", $"refs/heads/{head}");
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => Rows(commits).Any(item => item.Row.Subject == "first" && item.GridLabels.Count > 0));

        Rows(commits).Single(item => item.Row.Subject == "first").GridLabels
            .Where(label => label.IsMergeSource).Select(label => label.Text).Should().Equal($"origin/{head}");
    }

    [AvaloniaTest]
    public void The_view_menus_git_notes_choices_show_the_notes_after_the_body_or_in_their_column()
    {
        _repo.Run("notes", "add", "-m", "reviewed by Ann", "HEAD");
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        CommitListItem Head() => Rows(commits).First(item => item.Row.Subject == "second");
        WaitUntil(() => Rows(commits).Any(item => item.Row.Subject == "second"));
        Head().NotesText.Should().BeEmpty("the notes are read only while the grid shows them");
        window.Resources["ShowNotesColumn"].Should().Be(false);

        Click(window, "ShowGitNotesColumnMenuItem");

        WaitUntil(() => Head().NotesText == "reviewed by Ann");
        window.Resources["ShowNotesColumn"].Should().Be(true);
        Head().BodyText.Should().NotContain("reviewed", "with the column the notes are not in the message");

        Click(window, "ShowGitNotesColumnMenuItem");
        Click(window, "ShowGitNotesMenuItem");

        WaitUntil(() => Head().BodyText.Contains("reviewed by Ann", StringComparison.Ordinal));
        window.Resources["ShowNotesColumn"].Should().Be(false);
        TestAppBuilder.Preferences.GridViewOptions.ShowGitNotes.Should().BeTrue();
    }

    [AvaloniaTest]
    public void A_commit_whose_message_has_a_body_shows_upstreams_multi_line_indicator()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "m.txt"), "x");
        _repo.Run("add", "m.txt");
        _repo.Run("commit", "-q", "-m", "with body", "-m", "the body");
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => Rows(commits).Any(item => item.Row.Subject == "with body") && commits.GetRealizedContainers().Any());

        Indicator(commits, "with body").IsVisible.Should().BeTrue();
        Indicator(commits, "first").IsVisible.Should().BeFalse("its message is one line");
    }

    private static Border Indicator(ListBox commits, string subject)
        => commits.ContainerFromItem(Rows(commits).Single(item => item.Row.Subject == subject))!
            .GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("multilineIndicator"));

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
        TestAppBuilder.Preferences.RelativeDate = true;
        TestAppBuilder.Preferences.RevisionGraphDrawNonRelativesTextGray = false;
        TestAppBuilder.Preferences.RevisionGraphDrawNonRelativesGray = true;
        TestAppBuilder.Preferences.FillRefLabels = false;
        TestAppBuilder.Preferences.MulticolorBranches = true;
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
