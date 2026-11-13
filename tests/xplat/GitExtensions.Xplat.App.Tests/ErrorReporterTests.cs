using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using BugReporter.Serialization;
using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;
using DialogResult = System.Windows.Forms.DialogResult;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream's error reports (<c>BugReportInvoker</c>, <c>UIReporter</c>, <c>BugReportForm</c>) in the new shell.
/// </summary>
internal sealed class ErrorReporterTests
{
    // git's switch for its tests: every repository counts as owned by someone else.
    private const string AssumeDifferentOwner = "GIT_TEST_ASSUME_DIFFERENT_OWNER";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private readonly List<Window> _shown = [];
    private readonly HashSet<Window> _taken = [];
    private readonly List<int> _exits = [];
    private readonly List<string> _openedUrls = [];

    [SetUp]
    public void SetUp()
    {
        _shown.Clear();
        _taken.Clear();
        _exits.Clear();
        _openedUrls.Clear();
        ErrorReporter.Showing += OnShowing;
        ErrorReporter.Exit = _exits.Add;
        ErrorReporter.OpenUrl = _openedUrls.Add;
    }

    [TearDown]
    public void TearDown()
    {
        ErrorReporter.Showing -= OnShowing;
        ErrorReporter.Exit = Environment.Exit;
        ErrorReporter.OpenUrl = OsShellUtil.OpenUrlInDefaultBrowser;
        foreach (Window window in _shown)
        {
            window.Close();
        }
    }

    [AvaloniaTest]
    public void An_error_of_the_application_offers_a_bug_report_and_Quit_ends_the_application()
    {
        Task report = ErrorReporter.ReportAsync(new InvalidOperationException("outer", new ArgumentException("boom")),
            isTerminating: false);

        MessageBoxWindow box = Next<MessageBoxWindow>();
        box.Title.Should().Be("Error");
        Find<TextBlock>(box, "HeadingText").Text.Should().Be("boom", "upstream heads the dialog with the root error");
        Find<SelectableTextBlock>(box, "MessageText").Text.Should().Contain("you can report a bug");
        Labels(box).Should().Equal("Report bug!", "Ignore");

        Press(box, "Report bug!");
        BugReportWindow bugReport = Next<BugReportWindow>();
        Find<TextBox>(bugReport, "exceptionTextBox").Text.Should().Be("System.InvalidOperationException");
        Find<TextBox>(bugReport, "exceptionMessageTextBox").Text.Should().Be("outer");
        Find<Button>(bugReport, "IgnoreButton").IsVisible.Should()
            .BeFalse("only an external error can be ignored there");
        Find<Button>(bugReport, "sendAndQuitButton").IsEnabled.Should().BeTrue();

        UiClicks.Raise(Find<Button>(bugReport, "quitButton"), Button.ClickEvent);
        WaitUntil(() => report.IsCompleted);
        _exits.Should().Equal(-1);
    }

    [AvaloniaTest]
    public void A_git_error_offers_Ignore_first_and_its_details_without_a_report()
    {
        Task report = ErrorReporter.ReportAsync(new GitOperationException("fatal: bad revision 'x'"),
            isTerminating: false);

        MessageBoxWindow box = Next<MessageBoxWindow>();
        Labels(box).Should().Equal("Ignore", "View details");
        Descendants<TextBlock>(box).Select(text => text.Text).Should()
            .Contain("error from git or from other external application");

        Press(box, "View details");
        BugReportWindow details = Next<BugReportWindow>();
        Find<TabControl>(details, "mainTabs").SelectedItem.Should().Be(Find<TabItem>(details, "exceptionTabPage"));
        Find<Button>(details, "sendAndQuitButton").IsEnabled.Should()
            .BeFalse("upstream holds back the user's own errors");
        Button ignore = Find<Button>(details, "IgnoreButton");
        ignore.IsVisible.Should().BeTrue();
        UiClicks.Raise(ignore, Button.ClickEvent);

        WaitUntil(() => report.IsCompleted);
        details.Result.Should().Be(DialogResult.Ignore);
        _exits.Should().BeEmpty();
    }

    [AvaloniaTest]
    public void A_cancellation_shows_nothing()
    {
        Task report = ErrorReporter.ReportAsync(new OperationCanceledException(), isTerminating: false);

        report.IsCompleted.Should().BeTrue();
        _shown.Should().BeEmpty();
    }

    [AvaloniaTest]
    public void Send_and_Quit_needs_a_description_and_a_confirmation_then_opens_a_new_issue()
    {
        BugReportWindow window = new(new SerializableException(new InvalidOperationException("boom")), "",
            "- Git Extensions", canIgnore: true, showIgnore: false, focusDetails: false) { OpenUrl = _openedUrls.Add, };
        _shown.Add(window);
        window.Show();
        Button send = Find<Button>(window, "sendAndQuitButton");

        UiClicks.Raise(send, Button.ClickEvent);
        WaitUntil(() => window.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow missing = window.OwnedWindows.OfType<MessageBoxWindow>().Single();
        Find<SelectableTextBlock>(missing, "MessageText").Text.Should().StartWith("Please provide");
        missing.Close();
        WaitUntil(() => !window.OwnedWindows.Any());

        Find<TextBox>(window, "descriptionTextBox").Text = "Clicked Refresh twice.";
        UiClicks.Raise(send, Button.ClickEvent);
        WaitUntil(() => window.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow confirm = window.OwnedWindows.OfType<MessageBoxWindow>().Single();
        Descendants<Button>(confirm).Single(button => button.Name == "YesButton")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        WaitUntil(() => window.Result == DialogResult.Abort);
        _openedUrls.Should().ContainSingle().Which.Should()
            .StartWith("https://github.com/gitextensions/gitextensions/issues/new?title=%5BNBug%5D%20boom")
            .And.Contain("Clicked%20Refresh%20twice.");
    }

    [AvaloniaTest]
    public void A_report_that_cannot_be_ignored_does_not_close_from_the_title_bar()
    {
        BugReportWindow window = new(new SerializableException(new InvalidOperationException("fatal")), "", "",
            canIgnore: false, showIgnore: false, focusDetails: false);
        _shown.Add(window);
        window.Show();

        window.Close();
        Dispatcher.UIThread.RunJobs();

        window.IsVisible.Should().BeTrue("upstream hides the close box when the error ends the application");
        UiClicks.Raise(Find<Button>(window, "quitButton"), Button.ClickEvent);
        WaitUntil(() => !window.IsVisible);
        window.Result.Should().Be(DialogResult.Abort);
    }

    [AvaloniaTest]
    public void A_repository_git_does_not_trust_is_trusted_from_the_report_and_opened()
    {
        using TestRepository repo = new();
        string folder = Path.Combine(Path.GetTempPath(), "xplat-safe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string globalConfig = Path.Combine(folder, "global.gitconfig");
        string? oldGlobal = Environment.GetEnvironmentVariable("GIT_CONFIG_GLOBAL");
        string? oldOwner = Environment.GetEnvironmentVariable(AssumeDifferentOwner);
        MainWindow? window = null;
        try
        {
            Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", globalConfig);
            Environment.SetEnvironmentVariable(AssumeDifferentOwner, "1");
            window = new MainWindow(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
            window.Show();

            window.OpenRepository(repo.Path);
            MessageBoxWindow box = Next<MessageBoxWindow>();
            box.Title.Should().Be("Git security error");
            Labels(box).Should().StartWith(["Open repository in Explorer", "Trust this repository"]);
            Press(box, "Trust this repository");

            WaitUntil(() => File.Exists(globalConfig) && File.ReadAllText(globalConfig).Contains("[safe]"));
            WaitUntil(() => !window.IsBusy && window.FindControl<ListBox>("CommitList")?.ItemCount > 0);
            _shown.OfType<MessageBoxWindow>().Should()
                .ContainSingle("every failed read of the repository shows one report");
        }
        finally
        {
            window?.Close();
            Environment.SetEnvironmentVariable(AssumeDifferentOwner, oldOwner);
            Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", oldGlobal);
            Directory.Delete(folder, recursive: true);
        }
    }

    private static IEnumerable<T> Descendants<T>(Window window) => window.GetLogicalDescendants().OfType<T>();

    // The command links' and buttons' texts, without access key marks.
    private static IReadOnlyList<string> Labels(Window window)
        => [.. Descendants<Button>(window).Select(Label).OfType<string>()];

    private static string? Label(Button button)
        => button.GetLogicalDescendants().OfType<AccessText>().FirstOrDefault()?.Text?.Replace("_", "")
           ?? button.Content as string;

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"Control {name} not found");

    private static void Press(Window window, string label)
        => Descendants<Button>(window).Single(button => Label(button) == label)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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
            Thread.Sleep(10);
        }
    }

    // The first window of the report not returned yet, once it shows.
    private T Next<T>() where T : Window
    {
        WaitUntil(() => _shown.OfType<T>().FirstOrDefault(window => !_taken.Contains(window)) is { IsVisible: true });
        T next = _shown.OfType<T>().First(window => !_taken.Contains(window));
        _taken.Add(next);
        return next;
    }

    private void OnShowing(object? sender, Window window) => _shown.Add(window);
}
