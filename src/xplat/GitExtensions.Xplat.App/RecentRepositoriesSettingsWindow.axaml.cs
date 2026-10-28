using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitExtUtils.GitUI.Theming;
using HistoryEntry = GitCommands.UserRepositoryHistory.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormRecentReposSettings</c> (rules in <see cref="RecentRepositoriesSettingsDialog"/>), opened by the
///  working directory menu's "Configure this menu...": the recent repositories menus' settings, previewed in the top and
///  recent lists, where repositories are anchored or removed. Closes with true once OK stored the settings.
/// </summary>
public partial class RecentRepositoriesSettingsWindow : Window
{
    // Upstream's context menu.
    private const string AnchorToTopText = "Anchor to top repositories";
    private const string AnchorToRecentText = "Anchor to recent repositories";
    private const string RemoveAnchorText = "Remove anchor";
    private const string RemoveRecentText = "Remove from recent repositories";

    private readonly IRecentRepositoryStore _store;
    private readonly IAppPreferences _preferences;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IBrush _missingBrush;
    private RecentRepositoriesSettingsDialog? _dialog;
    private int _previousComboWidth;
    private bool _showing;

    public RecentRepositoriesSettingsWindow()
        : this(AppServices.RecentRepositories, AppServices.Preferences)
    {
    }

    public RecentRepositoriesSettingsWindow(IRecentRepositoryStore store, IAppPreferences preferences)
    {
        _store = store;
        _preferences = preferences;
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormRecentReposSettings");
        InitialFocus.Attach(this);
        System.Drawing.Color red = System.Drawing.Color.Red.AdaptForeColor(ThemeBrushes.Current.IsDark
            ? System.Drawing.Color.FromArgb(0x20, 0x20, 0x20)
            : System.Drawing.Color.White);
        _missingBrush = new SolidColorBrush(Color.FromArgb(red.A, red.R, red.G, red.B));
        TopLB.ItemTemplate = RowTemplate();
        RecentLB.ItemTemplate = RowTemplate();
        TopLB.ContextMenu = RepositoryMenu(TopLB);
        RecentLB.ContextMenu = RepositoryMenu(RecentLB);

        // Upstream's double-clicks move a repository to the other list.
        RecentLB.DoubleTapped += (_, _) => Anchor(RecentLB, HistoryEntry.RepositoryAnchor.AnchoredInTop);
        TopLB.DoubleTapped += (_, _) => Anchor(TopLB, HistoryEntry.RepositoryAnchor.AnchoredInRecent);
        foreach (CheckBox check in new[] { hideTopRepositoriesFromRecentList, sortTopRepos, sortRecentRepos })
        {
            check.IsCheckedChanged += (_, _) => SettingsChanged();
        }

        foreach (RadioButton radio in new[] { dontShortenRB, middleDotRB, mostSigDirRB })
        {
            radio.IsCheckedChanged += (_, _) => SettingsChanged();
        }

        _NO_TRANSLATE_maxRecentRepositories.ValueChanged += (_, _) => SettingsChanged();
        comboMinWidthEdit.ValueChanged += (_, _) => ComboMinWidthChanged();
        Ok.Click += (_, _) => Run(async () =>
        {
            if (_dialog is not null)
            {
                _dialog.HistorySize = (int)(_NO_TRANSLATE_RecentRepositoriesHistorySize.Value ?? _dialog.HistorySize);
                await _dialog.SaveAsync(_preferences);
            }

            Close(true);
        });
        Abort.Click += (_, _) => Close(false);
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  Completes when the settings and the lists are shown, for tests.
    /// </summary>
    public Task SettingsLoaded => _loaded.Task;

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));

    // Upstream's constructor and LoadSettings.
    private async Task LoadAsync()
    {
        _dialog = await RecentRepositoriesSettingsDialog.LoadAsync(_store, _preferences);
        RecentRepositoryMenuSettings settings = _dialog.Settings;
        _showing = true;
        _NO_TRANSLATE_maxRecentRepositories.Value = settings.MaxTopRepositories;
        _NO_TRANSLATE_RecentRepositoriesHistorySize.Value = _dialog.HistorySize;
        hideTopRepositoriesFromRecentList.IsChecked = settings.HideTopRepositoriesFromRecentList;
        sortTopRepos.IsChecked = settings.SortTopRepos;
        sortRecentRepos.IsChecked = settings.SortRecentRepos;
        comboMinWidthEdit.Value = settings.ComboMinWidth;
        _previousComboWidth = settings.ComboMinWidth;
        (settings.Shortening switch
        {
            ShorteningRecentRepoPathStrategy.MostSignDir => mostSigDirRB,
            ShorteningRecentRepoPathStrategy.MiddleDots => middleDotRB,
            _ => dontShortenRB,
        }).IsChecked = true;
        _showing = false;
        ShowRows();
        _loaded.TrySetResult();
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
            MaxTopRepositories = (int)(_NO_TRANSLATE_maxRecentRepositories.Value ?? 0),
            HideTopRepositoriesFromRecentList = hideTopRepositoriesFromRecentList.IsChecked == true,
            Shortening = mostSigDirRB.IsChecked == true ? ShorteningRecentRepoPathStrategy.MostSignDir
                : middleDotRB.IsChecked == true ? ShorteningRecentRepoPathStrategy.MiddleDots
                : ShorteningRecentRepoPathStrategy.None,
            SortTopRepos = sortTopRepos.IsChecked == true,
            SortRecentRepos = sortRecentRepos.IsChecked == true,
            ComboMinWidth = (int)(comboMinWidthEdit.Value ?? 0),
        };
        ShowRows();
    }

    // Upstream's comboMinWidthEdit_ValueChanged: setting the corrected width raises this again, which then keeps it.
    private void ComboMinWidthChanged()
    {
        int value = (int)(comboMinWidthEdit.Value ?? 0);
        int width = RecentRepositoriesSettingsDialog.ComboMinWidth(_previousComboWidth, value);
        _previousComboWidth = width;
        if (width != value)
        {
            comboMinWidthEdit.Value = width;
            return;
        }

        SettingsChanged();
    }

    // Upstream's RefreshRepos.
    private void ShowRows()
    {
        if (_dialog is null)
        {
            return;
        }

        (IReadOnlyList<RecentRepositorySettingsRow> top, IReadOnlyList<RecentRepositorySettingsRow> recent) =
            _dialog.Rows();
        TopLB.ItemsSource = top;
        RecentLB.ItemsSource = recent;
    }

    // Upstream's GetRepositoryListViewItem, in a column as wide as the combobox would be (upstream's SetComboWidth).
    private FuncDataTemplate<RecentRepositorySettingsRow> RowTemplate()
        => new((row, _) =>
        {
            TextBlock text = new() { TextTrimming = TextTrimming.CharacterEllipsis };
            if (row is null)
            {
                return text;
            }

            text.Text = row.Caption;
            text.FontWeight = row.Anchored ? FontWeight.Bold : FontWeight.Normal;
            if (row.Missing)
            {
                text.Foreground = _missingBrush;
            }

            if (_dialog?.Settings.ComboMinWidth is int width and > 0)
            {
                text.Width = Math.Max(RecentRepositoriesSettingsDialog.MinComboWidthAllowed, width);
                text.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            }

            ToolTip.SetTip(text, row.Path);
            return text;
        });

    // Upstream's contextMenuStrip1, shared by both lists; it opens only on a selection, and offers the anchors the last
    // selected repository does not have.
    private ContextMenu RepositoryMenu(ListBox list)
    {
        MenuItem anchorToTop = new()
        {
            Name = list.Name + "AnchorToTopMenuItem", Header = UpstreamTranslation.ByText(AnchorToTopText)
        };
        MenuItem anchorToRecent = new()
        {
            Name = list.Name + "AnchorToRecentMenuItem", Header = UpstreamTranslation.ByText(AnchorToRecentText)
        };
        MenuItem removeAnchor = new()
        {
            Name = list.Name + "RemoveAnchorMenuItem", Header = UpstreamTranslation.ByText(RemoveAnchorText)
        };
        MenuItem removeRecent = new()
        {
            Name = list.Name + "RemoveRecentMenuItem", Header = UpstreamTranslation.ByText(RemoveRecentText)
        };
        anchorToTop.Click += (_, _) => Anchor(list, HistoryEntry.RepositoryAnchor.AnchoredInTop);
        anchorToRecent.Click += (_, _) => Anchor(list, HistoryEntry.RepositoryAnchor.AnchoredInRecent);
        removeAnchor.Click += (_, _) => Anchor(list, HistoryEntry.RepositoryAnchor.None);
        removeRecent.Click += (_, _) => Run(async () =>
        {
            if (_dialog is not null && SelectedRows(list) is { Count: > 0 } rows)
            {
                await _dialog.RemoveAsync(rows.Select(row => row.Path));
                ShowRows();
            }
        });
        ContextMenu menu = new() { ItemsSource = new[] { anchorToTop, anchorToRecent, removeAnchor, removeRecent } };
        menu.Opening += (_, e) =>
        {
            if (_dialog is null || SelectedRows(list) is not { Count: > 0 } rows)
            {
                e.Cancel = true;
                return;
            }

            (anchorToTop.IsEnabled, anchorToRecent.IsEnabled, removeAnchor.IsEnabled) =
                RecentRepositoriesSettingsDialog.MenuState(_dialog.AnchorOf(rows[^1].Path));
        };
        return menu;
    }

    private void Anchor(ListBox list, HistoryEntry.RepositoryAnchor anchor)
    {
        if (_dialog is not null && SelectedRows(list) is { Count: > 0 } rows)
        {
            _dialog.Anchor(rows.Select(row => row.Path), anchor);
            ShowRows();
        }
    }

    // The selected repositories in the list's order, as upstream's ListView gives them.
    private static List<RecentRepositorySettingsRow> SelectedRows(ListBox list)
        => list.ItemsSource is IEnumerable<RecentRepositorySettingsRow> rows && list.SelectedItems is { } selected
            ? [.. rows.Where(selected.Contains)]
            : [];
}
