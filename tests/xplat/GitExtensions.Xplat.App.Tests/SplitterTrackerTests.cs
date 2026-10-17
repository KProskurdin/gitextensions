using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class SplitterTrackerTests
{
    private const string Form = "TestForm";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    [SetUp]
    public void Setup() => TestAppBuilder.Splitters.Clear();

    [TearDown]
    public void TearDown() => TestAppBuilder.Splitters.Clear();

    [AvaloniaTest]
    public void A_stored_splitter_is_restored_when_its_grid_is_laid_out_and_saved_when_the_window_closes()
    {
        TestAppBuilder.Splitters.Store(Form, "columns", new SplitterSettings(800, 96, 300, null, null));
        TestAppBuilder.Splitters.Store(Form, "rows", new SplitterSettings(606, 96, 150, null, null));
        Grid columns = new() { ColumnDefinitions = new ColumnDefinitions("200,6,*") };
        Grid rows = new() { RowDefinitions = new RowDefinitions("*,6,*") };
        Window window = new() { Width = 800, Height = 606, Content = new Panel { Children = { columns, rows } } };
        SplitterTracker tracker = new(window, TestAppBuilder.Splitters, Form);
        tracker.Add("columns", columns, rows: false, SplitterFixedPanel.Panel1);
        tracker.Add("rows", rows, rows: true, SplitterFixedPanel.None, splitterWidth: 6);

        window.Show();

        WaitUntil(() => columns.ColumnDefinitions[0].ActualWidth == 300);
        WaitUntil(() => Math.Abs(rows.RowDefinitions[0].ActualHeight - 150) < 1);
        rows.RowDefinitions[2].ActualHeight.Should().BeApproximately(450, 1, "both rows keep their proportion");
        columns.ColumnDefinitions[0].Width = new GridLength(250);
        Dispatcher.UIThread.RunJobs();
        window.Close();

        TestAppBuilder.Splitters.Saved[$"{Form}.columns"].Should().Be(new SplitterSettings(800, 96, 250, false, false));
        TestAppBuilder.Splitters.Saved[$"{Form}.rows"].Distance.Should().Be(150);
    }

    [AvaloniaTest]
    public void The_browse_window_restores_a_hidden_left_panel_and_keeps_its_width()
    {
        TestAppBuilder.Splitters.Store("FormBrowse", "MainSplitContainer",
            new SplitterSettings(1000, 96, 350, Panel1Collapsed: true, Panel2Collapsed: false));
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        ToggleButton toggle = Find<ToggleButton>(window, "ToggleLeftPanelButton");

        WaitUntil(() => toggle.IsChecked == false);
        Find<Grid>(window, "MainSplitGrid").ColumnDefinitions[0].Width.Value.Should().Be(0);
        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Find<Grid>(window, "MainSplitGrid").ColumnDefinitions[0].Width.Value.Should().Be(350);
        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.Close();

        SplitterSettings saved = TestAppBuilder.Splitters.Saved["FormBrowse.MainSplitContainer"];
        (saved.Distance, saved.Panel1Collapsed).Should().Be((350, true));
    }

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ??
           window.GetLogicalDescendants().OfType<T>().FirstOrDefault(control => control.Name == name) ??
           throw new InvalidOperationException($"Control {name} not found");

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
