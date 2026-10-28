using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AwesomeAssertions;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;
using HistoryEntry = GitCommands.UserRepositoryHistory.Repository;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class RecentRepositoriesSettingsWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private string _root = null!;
    private string _alpha = null!;
    private string _beta = null!;
    private string _missing = null!;
    private InMemoryRecentRepositoryStore _store = null!;
    private InMemoryAppPreferences _preferences = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        _root = Path.Combine(Path.GetTempPath(), "xplat-recent-" + Guid.NewGuid().ToString("N"));
        _alpha = Directory.CreateDirectory(Path.Combine(_root, "alpha")).FullName;
        _beta = Directory.CreateDirectory(Path.Combine(_root, "beta")).FullName;
        _missing = Path.Combine(_root, "gone");
        _store = new InMemoryRecentRepositoryStore();
        _preferences = new InMemoryAppPreferences
        {
            RecentRepositoryMenu = new RecentRepositoryMenuSettings(MaxTopRepositories: 1),
        };
        await _store.AddAsync(_missing);
        await _store.AddAsync(_beta);
        await _store.AddAsync(_alpha);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [AvaloniaTest]
    public async Task The_lists_follow_the_settings_and_OK_stores_them_with_the_anchors()
    {
        RecentRepositoriesSettingsWindow window = Show();
        ListBox top = Find<ListBox>(window, "TopLB");
        ListBox recent = Find<ListBox>(window, "RecentLB");
        Paths(top).Should().Equal(_alpha);
        Paths(recent).Should().Equal(_alpha, _beta, _missing);
        Rows(recent)[2].Missing.Should().BeTrue();

        Find<CheckBox>(window, "hideTopRepositoriesFromRecentList").IsChecked = true;
        Find<RadioButton>(window, "mostSigDirRB").IsChecked = true;

        Paths(recent).Should().Equal(_beta, _missing);
        Rows(top).Single().Caption.Should().Be("alpha");

        recent.SelectedItem = Rows(recent)[0];
        recent.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, null!));

        Rows(top).Should().Contain(row => row.Path == _beta && row.Anchored, "a double-click anchors to the top list");
        NumericUpDown width = Find<NumericUpDown>(window, "comboMinWidthEdit");
        width.Value = 10;
        width.Value.Should().Be(RecentRepositoriesSettingsDialog.MinComboWidthAllowed);
        width.Value = 20;
        width.Value.Should().Be(0, "going down below the minimum is autosize");
        width.Value = 50;
        Find<NumericUpDown>(window, "_NO_TRANSLATE_RecentRepositoriesHistorySize").Value = 45;

        Find<Button>(window, "Ok").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => !window.IsVisible);

        _preferences.RecentRepositoryMenu.Should().Be(new RecentRepositoryMenuSettings(1, true,
            ShorteningRecentRepoPathStrategy.MostSignDir, ComboMinWidth: 50));
        _preferences.RecentRepositoriesHistorySize.Should().Be(45);
        (await _store.LoadHistoryAsync()).Recent.Single(entry => entry.Path == _beta).Anchor
            .Should().Be(HistoryEntry.RepositoryAnchor.AnchoredInTop);
    }

    [AvaloniaTest]
    public void Cancel_stores_nothing()
    {
        RecentRepositoriesSettingsWindow window = Show();
        Find<CheckBox>(window, "sortTopRepos").IsChecked = true;

        Find<Button>(window, "Abort").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => !window.IsVisible);

        _preferences.RecentRepositoryMenu.SortTopRepos.Should().BeFalse();
    }

    [AvaloniaTest]
    public async Task The_context_menu_offers_the_other_anchors_and_removes_a_repository_at_once()
    {
        RecentRepositoriesSettingsWindow window = Show();
        ListBox recent = Find<ListBox>(window, "RecentLB");
        ContextMenu menu = recent.ContextMenu!;
        List<MenuItem> items = [.. menu.Items.OfType<MenuItem>()];
        items.Select(item => item.Header).Should().Equal("Anchor to top repositories", "Anchor to recent repositories",
            "Remove anchor", "Remove from recent repositories");

        recent.SelectedItem = Rows(recent)[1];
        recent.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        menu.IsOpen.Should().BeTrue();
        items.Select(item => item.IsEnabled).Should().Equal(true, true, false, true);
        menu.Close();

        items[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        Rows(recent)[1].Anchored.Should().BeTrue();
        Rows(recent)[1].Path.Should().Be(_beta);
        recent.SelectedItem = Rows(recent)[2];
        items[3].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => Paths(recent).Count == 2);
        (await _store.LoadAsync()).Should().Equal(_alpha, _beta);
        window.Close();
    }

    [AvaloniaTest]
    public void Anchored_rows_are_bold_and_missing_ones_red()
    {
        RecentRepositoriesSettingsWindow window = Show();
        ListBox recent = Find<ListBox>(window, "RecentLB");
        recent.SelectedItem = Rows(recent)[0];
        recent.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, null!));
        Dispatcher.UIThread.RunJobs();

        List<TextBlock> texts =
        [
            .. Find<ListBox>(window, "TopLB").GetRealizedContainers()
                .Select(container => ((ContentControl)container).Presenter?.Child).OfType<TextBlock>()
        ];
        string anchored = Rows(Find<ListBox>(window, "TopLB")).Single(row => row.Anchored).Caption;
        texts.Should().Contain(text => text.Text == anchored && text.FontWeight == FontWeight.Bold);
        TextBlock missing = recent.GetRealizedContainers()
            .Select(container => ((ContentControl)container).Presenter?.Child).OfType<TextBlock>()
            .Single(text => text.Text == Rows(recent).Single(row => row.Missing).Caption);
        missing.Foreground.Should().BeOfType<SolidColorBrush>().Which.Color.R.Should().BeGreaterThan(0x80);
        window.Close();
    }

    private RecentRepositoriesSettingsWindow Show()
    {
        RecentRepositoriesSettingsWindow window = new(_store, _preferences);
        window.Show();
        WaitUntil(() => window.SettingsLoaded.IsCompleted);
        window.Title.Should().Be("Recent repositories settings");
        return window;
    }

    private static IReadOnlyList<RecentRepositorySettingsRow> Rows(ListBox list)
        => [.. list.ItemsSource as IEnumerable<RecentRepositorySettingsRow> ?? []];

    private static IReadOnlyList<string> Paths(ListBox list) => [.. Rows(list).Select(row => row.Path)];

    private static T Find<T>(Window window, string name)
        where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"No control named {name}");

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
        }
    }
}
