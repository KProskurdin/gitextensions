using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream's FilterToolBar: the branch filter's "no results" item and the filter menu's shortcut texts.
/// </summary>
internal sealed class FilterToolBarTests
{
    private const string NoResults = "<No results found>";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private static readonly string[] _branches = ["main", "feature/One"];

    [TestCase("", "main", true)]
    [TestCase("ONE", "feature/One", true, Description = "whatever the case, as upstream")]
    [TestCase("one", "main", false)]
    [TestCase("", NoResults, false)]
    [TestCase("one", NoResults, false)]
    [TestCase("nothing", NoResults, true)]
    [TestCase(null, "main", true)]
    public void A_ref_shows_when_it_contains_the_text_and_no_results_when_none_does(string? search, string item, bool shown)
        => FilterToolBar.IsBranchFilterItemShown(search, item, _branches, NoResults).Should().Be(shown);

    [AvaloniaTest]
    public void The_filter_menu_shows_upstreams_hotkeys()
    {
        using TestRepository repo = new();
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(repo.Path);
        WaitUntil(() => !window.IsBusy);
        FilterToolBar toolBar = window.FindControl<FilterToolBar>("FilterToolStrip")!;

        toolBar.FindControl<ToolSplitButton>("AdvancedFilterButton")!.Flyout!.ShowAt(window);
        Dispatcher.UIThread.RunJobs();

        toolBar.FindControl<MenuItem>("ResetPathFilterMenuItem")!.InputGesture.Should()
            .Be(new KeyGesture(Key.H, Hotkeys.CommandModifier | KeyModifiers.Shift));
        toolBar.FindControl<MenuItem>("AdvancedFilterMenuItem")!.InputGesture.Should()
            .Be(new KeyGesture(Key.I, Hotkeys.CommandModifier));
        toolBar.FindControl<MenuItem>("ResetRevisionFiltersMenuItem")!.InputGesture.Should().NotBeNull();
        toolBar.FindControl<ToolSplitButton>("AdvancedFilterButton")!.Flyout!.Hide();
        window.Close();
    }

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
}
