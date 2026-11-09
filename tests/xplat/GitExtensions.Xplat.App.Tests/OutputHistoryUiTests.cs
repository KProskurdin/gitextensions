using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream's output history in the browse window: a tab, or a panel below the left panel.
/// </summary>
internal sealed class OutputHistoryUiTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;
    private OutputHistory _previous = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _previous = AppServices.OutputHistory;
        Reset();
    }

    [TearDown]
    public void TearDown()
    {
        AppServices.OutputHistory = _previous;
        Reset();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_output_tab_shows_the_history_follows_it_and_clears_it()
    {
        OutputHistory history = Use(new OutputHistory(20));
        history.RecordRun("Fetch origin", ["fetched"], DateTime.Now);
        MainWindow window = OpenWindow();

        Find<TabItem>(window, "OutputHistoryTab").IsVisible.Should().BeTrue();
        Find<Border>(window, "OutputHistoryPanel").IsVisible.Should().BeFalse();
        window.OutputHistoryText.Should().Contain("Fetch origin").And.Contain("fetched").And.EndWith(
            OutputHistory.EndMark + Environment.NewLine);

        history.Record("later", DateTime.Now);
        WaitUntil(() => window.OutputHistoryText!.Contains("later", StringComparison.Ordinal));

        Press(window, BrowseCommand.FocusOutputHistoryAndToggleIfPanel);
        Find<TabControl>(window, "DetailTabs").SelectedItem.Should().Be(Find<TabItem>(window, "OutputHistoryTab"));

        TextBox text = (TextBox)Find<TabItem>(window, "OutputHistoryTab").Content!;
        text.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Header as string == "Clear")
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => !window.OutputHistoryText!.Contains("Fetch origin", StringComparison.Ordinal));
        window.Close();
    }

    [AvaloniaTest]
    public void As_a_panel_the_hotkey_shows_and_hides_it_and_keeps_the_choice()
    {
        Use(new OutputHistory(20));
        TestAppBuilder.Preferences.ShowOutputHistoryAsTab = false;
        MainWindow window = OpenWindow();
        Border panel = Find<Border>(window, "OutputHistoryPanel");

        Find<TabItem>(window, "OutputHistoryTab").IsVisible.Should().BeFalse();
        panel.IsVisible.Should().BeFalse();

        Press(window, BrowseCommand.FocusOutputHistoryAndToggleIfPanel);

        panel.IsVisible.Should().BeTrue();
        TestAppBuilder.Preferences.OutputHistoryPanelVisible.Should().BeTrue();

        Press(window, BrowseCommand.FocusOutputHistoryAndToggleIfPanel);

        panel.IsVisible.Should().BeFalse();
        TestAppBuilder.Preferences.OutputHistoryPanelVisible.Should().BeFalse();
        window.Close();
    }

    [AvaloniaTest]
    public void A_depth_of_0_shows_neither_tab_nor_panel()
    {
        Use(new OutputHistory(0));
        TestAppBuilder.Preferences.OutputHistoryPanelVisible = true;
        TestAppBuilder.Preferences.ShowOutputHistoryAsTab = false;
        MainWindow window = OpenWindow();

        Find<TabItem>(window, "OutputHistoryTab").IsVisible.Should().BeFalse();
        Find<Border>(window, "OutputHistoryPanel").IsVisible.Should().BeFalse();
        window.OutputHistoryText.Should().BeNull();
        window.Close();
    }

    private static OutputHistory Use(OutputHistory history)
    {
        AppServices.OutputHistory = history;
        return history;
    }

    private static void Reset()
    {
        TestAppBuilder.Preferences.ShowOutputHistoryAsTab = true;
        TestAppBuilder.Preferences.OutputHistoryPanelVisible = false;
    }

    private MainWindow OpenWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        return window;
    }

    private static void Press(Window window, BrowseCommand command)
    {
        KeyGesture gesture = Hotkeys.GestureFor(command)!;
        window.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent, Key = gesture.Key, KeyModifiers = gesture.KeyModifiers,
        });
        Dispatcher.UIThread.RunJobs();
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
