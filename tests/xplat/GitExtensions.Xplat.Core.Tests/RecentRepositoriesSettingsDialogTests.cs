using AwesomeAssertions;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;
using HistoryEntry = GitCommands.UserRepositoryHistory.Repository;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RecentRepositoriesSettingsDialogTests
{
    private static readonly string _existing = Path.GetTempPath();
    private static readonly string _missing = Path.Combine(Path.GetTempPath(), "xplat-missing-" + Guid.NewGuid().ToString("N"));

    private InMemoryRecentRepositoryStore _store = null!;
    private InMemoryAppPreferences _preferences = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        _store = new InMemoryRecentRepositoryStore();
        _preferences = new InMemoryAppPreferences();
        await _store.AddAsync(_missing);
        await _store.AddAsync(_existing);
    }

    [Test]
    public async Task Rows_should_split_as_the_settings_ask_and_mark_anchors_and_missing_folders()
    {
        _preferences.RecentRepositoryMenu = new RecentRepositoryMenuSettings(MaxTopRepositories: 1);
        RecentRepositoriesSettingsDialog dialog = await RecentRepositoriesSettingsDialog.LoadAsync(_store, _preferences);

        dialog.Anchor([_missing], HistoryEntry.RepositoryAnchor.AnchoredInRecent);
        (IReadOnlyList<RecentRepositorySettingsRow> top, IReadOnlyList<RecentRepositorySettingsRow> recent) = dialog.Rows();

        top.Select(row => row.Path).Should().Equal(_existing);
        recent.Select(row => row.Path).Should().Equal(_existing, _missing);
        recent[1].Anchored.Should().BeTrue();
        recent[1].Missing.Should().BeTrue();
        top[0].Anchored.Should().BeFalse();
        top[0].Missing.Should().BeFalse();

        dialog.Settings = dialog.Settings with { HideTopRepositoriesFromRecentList = true };

        dialog.Rows().Recent.Select(row => row.Path).Should().Equal(_missing);
    }

    [Test]
    public async Task SaveAsync_should_store_the_settings_and_the_anchors()
    {
        RecentRepositoriesSettingsDialog dialog = await RecentRepositoriesSettingsDialog.LoadAsync(_store, _preferences);
        dialog.Anchor([_missing], HistoryEntry.RepositoryAnchor.AnchoredInTop);
        dialog.Settings = dialog.Settings with { Shortening = ShorteningRecentRepoPathStrategy.MostSignDir, SortTopRepos = true };
        dialog.HistorySize = 40;

        await dialog.SaveAsync(_preferences);

        _preferences.RecentRepositoryMenu.Should().Be(new RecentRepositoryMenuSettings(
            Shortening: ShorteningRecentRepoPathStrategy.MostSignDir, SortTopRepos: true));
        _preferences.RecentRepositoriesHistorySize.Should().Be(40);
        (await _store.LoadHistoryAsync()).Recent.Single(entry => entry.Path == _missing).Anchor
            .Should().Be(HistoryEntry.RepositoryAnchor.AnchoredInTop);
    }

    [Test]
    public async Task RemoveAsync_should_remove_at_once_and_read_the_list_back()
    {
        RecentRepositoriesSettingsDialog dialog = await RecentRepositoriesSettingsDialog.LoadAsync(_store, _preferences);
        dialog.Anchor([_existing], HistoryEntry.RepositoryAnchor.AnchoredInTop);

        await dialog.RemoveAsync([_missing]);

        (await _store.LoadAsync()).Should().Equal(_existing);
        dialog.AnchorOf(_existing).Should().Be(HistoryEntry.RepositoryAnchor.None, "upstream reads back the stored list");
    }

    [Test]
    public async Task LoadAsync_should_keep_the_values_within_upstreams_limits()
    {
        _preferences.RecentRepositoriesHistorySize = 3;
        _preferences.RecentRepositoryMenu = new RecentRepositoryMenuSettings(ComboMinWidth: 5000);

        RecentRepositoriesSettingsDialog dialog = await RecentRepositoriesSettingsDialog.LoadAsync(_store, _preferences);

        dialog.HistorySize.Should().Be(RecentRepositoriesSettingsDialog.MinHistorySize);
        dialog.Settings.ComboMinWidth.Should().Be(RecentRepositoriesSettingsDialog.MaxComboWidth);
    }

    [TestCase(0, 1, 30)]
    [TestCase(30, 29, 0)]
    [TestCase(0, 40, 40)]
    [TestCase(10, 10, 10)]
    public void ComboMinWidth_should_skip_the_widths_below_upstreams_minimum(int previous, int value, int expected)
        => RecentRepositoriesSettingsDialog.ComboMinWidth(previous, value).Should().Be(expected);

    [Test]
    public void MenuState_should_offer_the_anchors_the_repository_does_not_have()
    {
        RecentRepositoriesSettingsDialog.MenuState(HistoryEntry.RepositoryAnchor.None).Should().Be((true, true, false));
        RecentRepositoriesSettingsDialog.MenuState(HistoryEntry.RepositoryAnchor.AnchoredInTop).Should().Be((false, true, true));
    }
}
