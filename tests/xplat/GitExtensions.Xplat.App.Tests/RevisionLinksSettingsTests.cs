using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitCommands;
using GitCommands.Settings;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class RevisionLinksSettingsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        TestAppBuilder.UseRevisionLinks();
    }

    [TearDown]
    public void TearDown()
    {
        File.Delete(TestAppBuilder.RevisionLinksFile);
        TestAppBuilder.UseRevisionLinks();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_revision_links_tab_adds_and_stores_a_definition()
    {
        MainWindow window = NewWindow();
        window.Show();
        SettingsWindow settings = OpenSettings(window);
        Find<ListBox>(settings, "RevisionLinksList").ItemCount.Should().Be(0);
        Find<StackPanel>(settings, "RevisionLinkDetails").IsEnabled.Should().BeFalse();
        Find<StackPanel>(settings, "RevisionLinkTemplateButtons").Children.OfType<Button>()
            .Select(button => button.Content as string).Should()
            .Equal("Add GitHub templates", "Add Azure DevOps templates");

        Click(settings, "AddRevisionLinkButton");
        Find<TextBox>(settings, "RevisionLinkNameBox").Text.Should().Be("<new>");
        Find<TextBox>(settings, "RevisionLinkNameBox").Text = "Issues";
        Find<TextBox>(settings, "RevisionLinkSearchPatternBox").Text = "#\\d+";
        Find<TextBox>(settings, "RevisionLinkNestedPatternBox").Text = "\\d+";
        Find<CheckBox>(settings, "RevisionLinkLocalBranchCheck").IsChecked = true;
        Click(settings, "AddRevisionLinkFormatButton");
        RevisionLinkItem item = settings.RevisionLinkEditor.Items.Single();
        item.Formats.Should().ContainSingle();
        item.Formats[0].Caption = "#{0}";
        item.Formats[0].Format = "https://example.com/issues/{0}";
        Click(settings, "AddRevisionLinkFormatButton");
        Find<TabControl>(settings, "Tabs").SelectedItem = Find<TabItem>(settings, "RevisionLinksTab");
        Button? removeSecond = null;
        WaitUntil(() => (removeSecond = settings.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Name == "RemoveRevisionLinkFormatButton").Skip(1).FirstOrDefault()) is not null);
        removeSecond!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        item.Formats.Select(row => row.Caption).Should().Equal("#{0}");
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        RevisionLinkItem stored = new RevisionLinkEditor(AppServices.RevisionLinks.Open(null)).Items.Single();
        (stored.Name, stored.SearchPattern, stored.NestedSearchPattern, stored.SearchMessage,
                stored.SearchLocalBranches)
            .Should().Be(("Issues", "#\\d+", "\\d+", true, true));
        stored.Formats.Single().Format.Should().Be("https://example.com/issues/{0}");

        SettingsWindow again = OpenSettings(window);
        Find<TextBox>(again, "RevisionLinkNameBox").Text.Should().Be("Issues");
        Click(again, "RemoveRevisionLinkButton");
        Find<ListBox>(again, "RevisionLinksList").ItemCount.Should().Be(0);
        Click(again, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());
        new RevisionLinkEditor(AppServices.RevisionLinks.Open(null)).Items.Should().BeEmpty();
    }

    [AvaloniaTest]
    public void The_commit_details_list_the_links_of_the_message_and_of_the_branches_at_the_commit()
    {
        DistributedSettings stored = AppServices.RevisionLinks.Open(null);
        RevisionLinkEditor editor = new(stored);
        RevisionLinkItem issues = editor.Add();
        issues.Name = "Issues";
        issues.SearchLocalBranches = true;
        issues.SearchPattern = "(#|fix-)\\d+";
        issues.NestedSearchPattern = "\\d+";
        RevisionLinkFormatItem format = issues.AddFormat();
        format.Caption = "#{0}";
        format.Format = "https://example.com/issues/{0}";
        editor.Save();
        AppServices.RevisionLinks.Save(stored);
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "three");
        _repo.Run("commit", "-q", "-am", "Fix #42");
        _repo.Run("branch", "fix-77");

        MainWindow window = NewWindow();
        window.Show();
        Find<TextBox>(window, "PathBox").Text = _repo.Path;
        Click(window, "OpenButton");
        WaitUntil(() => Find<Button>(window, "OpenButton").IsEnabled);
        Find<ListBox>(window, "CommitList").SelectedIndex = 0;
        WaitUntil(() => Find<StackPanel>(window, "DetailLinksPanel").IsVisible);

        Find<WrapPanel>(window, "DetailLinks").Children.OfType<HyperlinkButton>()
            .Select(link => (link.Content as string, link.NavigateUri?.ToString())).Should().BeEquivalentTo(
            [
                ("#77", "https://example.com/issues/77"),
                ("#42", "https://example.com/issues/42"),
            ]);

        Find<ListBox>(window, "CommitList").SelectedIndex = 1;
        WaitUntil(() => !Find<StackPanel>(window, "DetailLinksPanel").IsVisible);
    }

    private static SettingsWindow OpenSettings(MainWindow window)
    {
        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);
        return settings;
    }

    private static MainWindow NewWindow() =>
        new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));

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
