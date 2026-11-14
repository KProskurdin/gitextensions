using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitExtUtils.GitUI.Theming;
using HistoryEntry = GitCommands.UserRepositoryHistory.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormRecentReposSettings</c>, bound to <see cref="RecentRepositoriesSettingsViewModel"/> (rules in
///  <see cref="RecentRepositoriesSettingsDialog"/>), opened by the working directory menu's "Configure this menu...": the
///  recent repositories menus' settings, previewed in the top and recent lists, where repositories are anchored or
///  removed. Closes with true once OK stored the settings.
/// </summary>
public partial class RecentRepositoriesSettingsWindow : Window
{
    // Upstream's context menu.
    private const string AnchorToTopText = "Anchor to top repositories";
    private const string AnchorToRecentText = "Anchor to recent repositories";
    private const string RemoveAnchorText = "Remove anchor";
    private const string RemoveRecentText = "Remove from recent repositories";

    private readonly IBrush _missingBrush;

    public RecentRepositoriesSettingsWindow()
        : this(AppServices.RecentRepositories, AppServices.Preferences)
    {
    }

    public RecentRepositoriesSettingsWindow(IRecentRepositoryStore store, IAppPreferences preferences)
    {
        ViewModel = new RecentRepositoriesSettingsViewModel(new WindowDialogHost(this), store, preferences);
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, RecentRepositoriesSettingsViewModel.Category);
        InitialFocus.Attach(this);

        // View concerns: the rows in upstream's colors and width, the lists' menus and double-clicks, which act on the
        // selected rows.
        System.Drawing.Color red = System.Drawing.Color.Red.AdaptForeColor(ThemeBrushes.Current.IsDark
            ? System.Drawing.Color.FromArgb(0x20, 0x20, 0x20)
            : System.Drawing.Color.White);
        _missingBrush = new SolidColorBrush(Color.FromArgb(red.A, red.R, red.G, red.B));
        TopLB.ItemTemplate = RowTemplate();
        RecentLB.ItemTemplate = RowTemplate();
        TopLB.ContextMenu = RepositoryMenu(TopLB);
        RecentLB.ContextMenu = RepositoryMenu(RecentLB);

        // Upstream's double-clicks move a repository to the other list.
        RecentLB.DoubleTapped += (_, _) =>
            ViewModel.Anchor(SelectedRows(RecentLB), HistoryEntry.RepositoryAnchor.AnchoredInTop);
        TopLB.DoubleTapped += (_, _) =>
            ViewModel.Anchor(SelectedRows(TopLB), HistoryEntry.RepositoryAnchor.AnchoredInRecent);
        Opened += (_, _) => _ = ViewModel.LoadAsync();
    }

    public RecentRepositoriesSettingsViewModel ViewModel { get; }

    /// <summary>
    ///  Completes when the settings and the lists are shown, for tests.
    /// </summary>
    public Task SettingsLoaded => ViewModel.Loaded;

    // The selected repositories in the list's order, as upstream's ListView gives them.
    private static List<RecentRepositorySettingsRow> SelectedRows(ListBox list)
        => list.ItemsSource is IEnumerable<RecentRepositorySettingsRow> rows && list.SelectedItems is { } selected
            ? [.. rows.Where(selected.Contains)]
            : [];

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

            if (ViewModel.RowWidth is { } width)
            {
                text.Width = width;
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
        anchorToTop.Click += (_, _) =>
            ViewModel.Anchor(SelectedRows(list), HistoryEntry.RepositoryAnchor.AnchoredInTop);
        anchorToRecent.Click += (_, _) =>
            ViewModel.Anchor(SelectedRows(list), HistoryEntry.RepositoryAnchor.AnchoredInRecent);
        removeAnchor.Click += (_, _) => ViewModel.Anchor(SelectedRows(list), HistoryEntry.RepositoryAnchor.None);
        removeRecent.Click += (_, _) => _ = ViewModel.RemoveAsync(SelectedRows(list));
        ContextMenu menu = new() { ItemsSource = new[] { anchorToTop, anchorToRecent, removeAnchor, removeRecent } };
        menu.Opening += (_, e) =>
        {
            if (ViewModel.MenuState(SelectedRows(list)) is not { } state)
            {
                e.Cancel = true;
                return;
            }

            (anchorToTop.IsEnabled, anchorToRecent.IsEnabled, removeAnchor.IsEnabled) = state;
        };
        return menu;
    }
}
