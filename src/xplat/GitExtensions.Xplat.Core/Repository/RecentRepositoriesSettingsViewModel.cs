using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;
using HistoryEntry = GitCommands.UserRepositoryHistory.Repository;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The view model of upstream's <c>FormRecentReposSettings</c> (rules in <see cref="RecentRepositoriesSettingsDialog"/>):
///  the recent repositories menus' settings, previewed in the top and recent lists, where repositories are anchored or
///  removed. Closes its window with true once OK stored the settings.
/// </summary>
public sealed partial class RecentRepositoriesSettingsViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormRecentReposSettings";

    private readonly IRecentRepositoryStore _store;
    private readonly IAppPreferences _preferences;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private RecentRepositoriesSettingsDialog? _dialog;
    private int _previousComboWidth;
    private bool _showing = true;

    public RecentRepositoriesSettingsViewModel(IDialogHost host, IRecentRepositoryStore store, IAppPreferences preferences)
        : base(host)
    {
        _store = store;
        _preferences = preferences;
    }

    [ObservableProperty]
    public partial decimal? MaxTopRepositories { get; set; }

    [ObservableProperty]
    public partial decimal? HistorySize { get; set; }

    [ObservableProperty]
    public partial bool HideTopRepositoriesFromRecentList { get; set; }

    [ObservableProperty]
    public partial bool SortTopRepos { get; set; }

    [ObservableProperty]
    public partial bool SortRecentRepos { get; set; }

    [ObservableProperty]
    public partial ShorteningRecentRepoPathStrategy Shortening { get; set; }

    /// <summary>
    ///  Upstream's combobox minimum width (0 = autosize), which the lists' rows take as their width.
    /// </summary>
    [ObservableProperty]
    public partial decimal? ComboMinWidth { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<RecentRepositorySettingsRow> TopRows { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<RecentRepositorySettingsRow> RecentRows { get; private set; } = [];

    /// <summary>
    ///  The rows' width as the combobox would be (upstream's <c>SetComboWidth</c>); null to fill the list.
    /// </summary>
    public double? RowWidth => _dialog?.Settings.ComboMinWidth is int width and > 0
        ? Math.Max(RecentRepositoriesSettingsDialog.MinComboWidthAllowed, width)
        : null;

    /// <summary>
    ///  Completes when the settings and the lists are shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Upstream's constructor and <c>LoadSettings</c>. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        _dialog = await RecentRepositoriesSettingsDialog.LoadAsync(_store, _preferences);
        RecentRepositoryMenuSettings settings = _dialog.Settings;
        _showing = true;
        MaxTopRepositories = settings.MaxTopRepositories;
        HistorySize = _dialog.HistorySize;
        HideTopRepositoriesFromRecentList = settings.HideTopRepositoriesFromRecentList;
        SortTopRepos = settings.SortTopRepos;
        SortRecentRepos = settings.SortRecentRepos;
        ComboMinWidth = settings.ComboMinWidth;
        _previousComboWidth = settings.ComboMinWidth;
        Shortening = settings.Shortening;
        _showing = false;
        ShowRows();
        _loaded.TrySetResult();
    });

    // Upstream's contextMenuStrip1 texts.
    public const string AnchorToTopText = "Anchor to top repositories";
    public const string AnchorToRecentText = "Anchor to recent repositories";
    public const string RemoveAnchorText = "Remove anchor";
    public const string RemoveRecentText = "Remove from recent repositories";

    /// <summary>
    ///  Upstream's contextMenuStrip1, shared by both lists: the anchors and the removal, for the repositories
    ///  <paramref name="rows"/> gives (the list's selection when an item runs). The items' names start with
    ///  <paramref name="namePrefix"/>, the list's name; their texts are plain headers, as upstream's.
    /// </summary>
    public IReadOnlyList<MenuEntry> MenuEntries(string namePrefix, Func<IReadOnlyList<RecentRepositorySettingsRow>> rows) =>
    [
        new(Host.Translate(AnchorToTopText), AnchorMenuRowsCommand,
            new RecentRepositoriesMenuTarget(rows, HistoryEntry.RepositoryAnchor.AnchoredInTop))
        {
            HasAccessKey = true, Name = namePrefix + "AnchorToTopMenuItem",
        },
        new(Host.Translate(AnchorToRecentText), AnchorMenuRowsCommand,
            new RecentRepositoriesMenuTarget(rows, HistoryEntry.RepositoryAnchor.AnchoredInRecent))
        {
            HasAccessKey = true, Name = namePrefix + "AnchorToRecentMenuItem",
        },
        new(Host.Translate(RemoveAnchorText), AnchorMenuRowsCommand,
            new RecentRepositoriesMenuTarget(rows, HistoryEntry.RepositoryAnchor.None))
        {
            HasAccessKey = true, Name = namePrefix + "RemoveAnchorMenuItem",
        },
        new(Host.Translate(RemoveRecentText), RemoveMenuRowsCommand, rows)
        {
            HasAccessKey = true, Name = namePrefix + "RemoveRecentMenuItem",
        },
    ];

    /// <summary>
    ///  As the menu of <see cref="MenuEntries"/> opens: false when it does not open (no selection); otherwise the anchors
    ///  the last selected repository does not have are enabled.
    /// </summary>
    public bool RefreshMenu(IReadOnlyList<MenuEntry> entries, IReadOnlyList<RecentRepositorySettingsRow> rows)
    {
        if (MenuState(rows) is not { } state)
        {
            return false;
        }

        (entries[0].IsEnabled, entries[1].IsEnabled, entries[2].IsEnabled) = state;
        return true;
    }

    /// <summary>
    ///  Upstream's menu state for the last selected repository: which anchors it offers.
    /// </summary>
    public (bool AnchorToTop, bool AnchorToRecent, bool RemoveAnchor)? MenuState(IReadOnlyList<RecentRepositorySettingsRow> rows)
        => _dialog is null || rows.Count == 0
            ? null
            : RecentRepositoriesSettingsDialog.MenuState(_dialog.AnchorOf(rows[^1].Path));

    /// <summary>
    ///  Anchors the repositories to a list, or removes their anchor; the lists show it.
    /// </summary>
    public void Anchor(IReadOnlyList<RecentRepositorySettingsRow> rows, HistoryEntry.RepositoryAnchor anchor)
    {
        if (_dialog is not null && rows.Count > 0)
        {
            _dialog.Anchor(rows.Select(row => row.Path), anchor);
            ShowRows();
        }
    }

    /// <summary>
    ///  Upstream's "Remove from recent repositories".
    /// </summary>
    public Task RemoveAsync(IReadOnlyList<RecentRepositorySettingsRow> rows) => GuardAsync(async () =>
    {
        if (_dialog is not null && rows.Count > 0)
        {
            await _dialog.RemoveAsync(rows.Select(row => row.Path));
            ShowRows();
        }
    });

    partial void OnMaxTopRepositoriesChanged(decimal? value) => SettingsChanged();

    partial void OnHideTopRepositoriesFromRecentListChanged(bool value) => SettingsChanged();

    partial void OnSortTopReposChanged(bool value) => SettingsChanged();

    partial void OnSortRecentReposChanged(bool value) => SettingsChanged();

    partial void OnShorteningChanged(ShorteningRecentRepoPathStrategy value) => SettingsChanged();

    // Upstream's comboMinWidthEdit_ValueChanged: setting the corrected width raises this again, which then keeps it.
    partial void OnComboMinWidthChanged(decimal? value)
    {
        if (_showing)
        {
            return;
        }

        int requested = (int)(value ?? 0);
        int width = RecentRepositoriesSettingsDialog.ComboMinWidth(_previousComboWidth, requested);
        _previousComboWidth = width;
        if (width != requested)
        {
            ComboMinWidth = width;
            return;
        }

        SettingsChanged();
    }

    // Upstream's sortTopRepos_CheckedChanged, which every setting of the lists raises: the lists show the new settings.
    private void SettingsChanged()
    {
        if (_showing || _dialog is null)
        {
            return;
        }

        _dialog.Settings = _dialog.Settings with
        {
            MaxTopRepositories = (int)(MaxTopRepositories ?? 0),
            HideTopRepositoriesFromRecentList = HideTopRepositoriesFromRecentList,
            Shortening = Shortening,
            SortTopRepos = SortTopRepos,
            SortRecentRepos = SortRecentRepos,
            ComboMinWidth = (int)(ComboMinWidth ?? 0),
        };
        ShowRows();
    }

    // Upstream's RefreshRepos.
    private void ShowRows()
    {
        if (_dialog is null)
        {
            return;
        }

        (TopRows, RecentRows) = _dialog.Rows();
        OnPropertyChanged(nameof(RowWidth));
    }

    [RelayCommand]
    private Task OkAsync() => GuardAsync(async () =>
    {
        if (_dialog is not null)
        {
            _dialog.HistorySize = (int)(HistorySize ?? _dialog.HistorySize);
            await _dialog.SaveAsync(_preferences);
        }

        Host.Close(true);
    });

    [RelayCommand]
    private void Cancel() => Host.Close(false);

    [RelayCommand]
    private void AnchorMenuRows(RecentRepositoriesMenuTarget target) => Anchor(target.Rows(), target.Anchor);

    [RelayCommand]
    private Task RemoveMenuRowsAsync(Func<IReadOnlyList<RecentRepositorySettingsRow>> rows) => RemoveAsync(rows());
}

/// <summary>
///  What an anchor item of the recent repositories' menu acts on: the list's selection, read when it runs, and the anchor.
/// </summary>
public sealed record RecentRepositoriesMenuTarget(Func<IReadOnlyList<RecentRepositorySettingsRow>> Rows,
    HistoryEntry.RepositoryAnchor Anchor);
