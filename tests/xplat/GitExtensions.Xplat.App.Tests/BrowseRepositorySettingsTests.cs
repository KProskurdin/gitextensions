using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class BrowseRepositorySettingsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        TestAppBuilder.Preferences.ShowRevisionGridTooltips = true;
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.ShowRevisionGridTooltips = true;
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_grid_shows_upstreams_tooltips_until_the_setting_turns_them_off()
    {
        string branch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        Find<TextBox>(window, "PathBox").Text = _repo.Path;
        Click(window, "OpenButton");
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => commits.ItemCount >= 2);

        RevisionTooltip tooltip = commits.Items.OfType<CommitListItem>().First().Row.Tooltip!;
        tooltip.Message.Should().Contain("body line").And.Contain($"[{branch}]");
        tooltip.Author.Should().Be("Test <test@example.com> authored and committed");
        tooltip.Hash.Should().Be(_repo.Run("rev-parse", "HEAD").Trim());
        WaitUntil(() => commits.GetVisualDescendants().OfType<TextBlock>()
            .Any(text => ToolTip.GetTip(text) as string == tooltip.Author));
        ToolTip.GetServiceEnabled(commits).Should().BeTrue();

        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);
        Find<CheckBox>(settings, "ShowRevisionTooltipsCheck").IsChecked.Should().BeTrue();
        Find<CheckBox>(settings, "ShowRevisionTooltipsCheck").IsChecked = false;
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        // The window applies the setting after the settings window has closed.
        WaitUntil(() => !ToolTip.GetServiceEnabled(commits));
        TestAppBuilder.Preferences.ShowRevisionGridTooltips.Should().BeFalse();
    }

    [AvaloniaTest]
    public void The_grid_draws_upstreams_graph_and_the_detailed_tab_saves_its_settings()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        Find<TextBox>(window, "PathBox").Text = _repo.Path;
        Click(window, "OpenButton");
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => commits.ItemCount >= 2);
        GraphRowRef head = commits.Items.OfType<CommitListItem>().First().Graph;
        (head.Index, head.IsHead, head.HasRefs, head.Graph.LaneCount).Should().Be((0, true, true, 1));
        WaitUntil(() => commits.GetVisualDescendants().OfType<GraphCell>().Any(cell => cell.Bounds.Width > 0));

        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);
        Find<CheckBox>(settings, "MergeGraphLanesCheck").IsChecked.Should().BeTrue();
        Find<CheckBox>(settings, "GraphDiagonalsCheck").IsChecked = false;
        Find<CheckBox>(settings, "StraightenDiagonalsCheck").IsChecked = false;
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        TestAppBuilder.Preferences.Should().BeEquivalentTo(new
        {
            MergeGraphLanesHavingCommonParent = true,
            RenderGraphWithDiagonals = false,
            StraightenGraphDiagonals = false,
        });
        TestAppBuilder.Preferences.RenderGraphWithDiagonals = true;
        TestAppBuilder.Preferences.StraightenGraphDiagonals = true;
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
