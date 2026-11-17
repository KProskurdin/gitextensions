using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

// Upstream's Dashboard and UserRepositoriesList, shown while no repository is open. The tiles' and the groups' menus and
// their commands are the view model's (DashboardViewModel.Menus, MVVM.md); the window opens them and asks its questions.
public partial class MainWindow : IDashboardWindow
{
    // Upstream's Dashboard links.
    private const string IssuesUrl = BrowseToolbarViewModel.IssuesUrl;

    // Upstream's Dashboard, UserRepositoriesList and FormDashboardCategoryTitle texts.
    private const string CloneForkText = "Clone {0} repository";
    private const string ActionsText = "Actions";
    private const string EnterCaptionText = "Enter Caption";
    private const string CategoryNameText = "Category name";
    private const string CategoryNameRequiredText = "Category name is required";
    private const string CategoryNameExistsText = "Category name already exists";

    // Upstream's tiles are as wide as the longest caption, its image and 50 pixels, and at least 50 pixels high.
    private const double TileExtraWidth = 50;
    private const double TileMinHeight = 50;

    // Upstream's TranslatedStrings.DirectoryInvalidRepository and UserRepositoriesList's _cannotOpenTheFolder.
    public const string InvalidRepositoryText = "The selected item is not a valid git repository.";
    public const string CannotOpenFolderCaption = "Cannot open the folder";

    private DashboardViewModel? _dashboardModel;
    private IReadOnlyList<string> _dashboardPaths = [];

    private DashboardViewModel Dashboard => _dashboardModel ??= new DashboardViewModel(AppServices.RecentRepositories,
        path =>
        {
            bool valid = GitModule.IsValidGitWorkingDir(path);
            return new DashboardRepositoryState(valid,
                valid && AppServices.Preferences.ShowRepoCurrentBranch ? RecentRepositoryBranches.CurrentBranch(path) : null);
        }, translate: UpstreamTranslation.ByText) { Window = this };

    // One folder of the dropped items, as upstream's dashboard takes.
    private static string? DroppedFolder(IDataTransfer data)
        => data.TryGetFiles() is [{ } item] && item.TryGetLocalPath() is { Length: > 0 } path && Directory.Exists(path)
            ? path
            : null;

    private async Task OpenDroppedFolderAsync(string folder)
    {
        if (!GitModule.IsValidGitWorkingDir(folder))
        {
            await new MessageBoxWindow(UpstreamTranslation.ByText(InvalidRepositoryText),
                UpstreamTranslation.ByText(CannotOpenFolderCaption), System.Windows.Forms.MessageBoxButtons.OK)
                .ShowDialog(this);
            return;
        }

        await OpenRepositoryAsync(folder);
    }

    private void WireDashboard()
    {
        foreach (Button link in DashboardStart.Children.Concat(DashboardContribute.Children).OfType<Button>())
        {
            link.Content = LinkContent(link.Tag as string, link.Content as string ?? "");
        }

        // Upstream's UserRepositoriesList OnDragEnter and OnDragDrop: one folder dropped on the list opens, or upstream's
        // message says it is not a repository.
        DragDrop.SetAllowDrop(DashboardRepositories, true);
        DashboardRepositories.AddHandler(DragDrop.DragOverEvent, (_, e) =>
            e.DragEffects = DroppedFolder(e.DataTransfer) is null ? DragDropEffects.None : DragDropEffects.Copy);
        DashboardRepositories.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            if (DroppedFolder(e.DataTransfer) is { } folder)
            {
                Run(() => OpenDroppedFolderAsync(folder));
            }
        });

        // Upstream: Enter opens the first repository shown, Down moves into the list.
        DashboardSearchBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Dashboard.Groups.SelectMany(group => group.Repositories).FirstOrDefault() is { } first)
            {
                e.Handled = true;
                OpenDashboardRepository(first);
            }
            else if (e.Key == Key.Down && DashboardTiles().FirstOrDefault() is { } tile)
            {
                e.Handled = true;
                tile.Focus();
            }
        };
        Dashboard.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.Groups))
            {
                ShowDashboard();
            }
        };

        // The groups and tiles are templates bound to the view model; their sizes and the group link's text are resources.
        Resources["DashboardActionsText"] = UpstreamTranslation.ByText(ActionsText);
        ShowDashboard();
    }

    // Upstream adds a "Clone <host> repository" link for each repository host plugin.
    private void ShowDashboardHosts(IEnumerable<IRepositoryHostPlugin> hosts)
    {
        foreach (Button link in DashboardStart.Children.OfType<Button>().Where(link => link.Name is null).ToList())
        {
            DashboardStart.Children.Remove(link);
        }

        foreach (IRepositoryHostPlugin host in hosts)
        {
            Button link = new()
            {
                Classes = { "dashboardLink" },
                Content = LinkContent("CloneRepoGitHub",
                    string.Format(UpstreamTranslation.ByText(CloneForkText), host.Name)),
                Command = Toolbar.CloneFromHostCommand,
                CommandParameter = host.Name,
            };
            DashboardStart.Children.Add(link);
        }
    }

    // The recent list changed: the dashboard reads the history again when its repositories differ.
    private void ReloadDashboardIfChanged()
    {
        IReadOnlyList<string> paths = [.. _recent.Items.Select(item => item.Path)];
        if (!paths.SequenceEqual(_dashboardPaths))
        {
            _dashboardPaths = paths;
            Run(Dashboard.LoadAsync);
        }
    }

    // Upstream's tiles are as wide as the longest caption, and show the branch a point smaller.
    private void ShowDashboard()
    {
        Resources["DashboardTileWidth"] = TileWidth(Dashboard.Groups.SelectMany(group => group.Repositories));
        Resources["DashboardBranchFontSize"] = Math.Max(1, FontSize - 1.33);
    }

    // A tile's menu is made for its repository, as upstream's: when the template is made with the repository, and when a
    // container takes another one. A tile the list no longer shows keeps its menu.
    private void DashboardTile_DataContextChanged(object? sender, EventArgs e) => ShowTileMenu(sender);

    private void DashboardTile_AttachedToLogicalTree(object? sender, LogicalTreeAttachmentEventArgs e) => ShowTileMenu(sender);

    private void ShowTileMenu(object? sender)
    {
        if (sender is Button { DataContext: DashboardRepository repository } tile
            && !Equals(tile.ContextMenu?.Tag, repository))
        {
            ContextMenu menu = new() { Tag = repository };
            MenuEntries.Fill(menu.Items, Dashboard.RepositoryMenu(repository), this);

            // The categories and the missing repositories are read again as the menu opens, as upstream's.
            menu.Opening += (_, _) => MenuEntries.Fill(menu.Items, Dashboard.RepositoryMenu(repository), this);
            tile.ContextMenu = menu;
        }
    }

    // The group's task link opens its menu below it; the menu's items are the view model's.
    private void DashboardGroupActions_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: DashboardGroup group } actions)
        {
            ContextMenu menu = new();
            MenuEntries.Fill(menu.Items, Dashboard.GroupMenu(group), this);
            actions.ContextMenu = menu;
            menu.Open(actions);
        }
    }

    private double TileWidth(IEnumerable<DashboardRepository> repositories)
    {
        // Upstream's RecentReposComboMinWidth, when set, is the width of the text.
        int minWidth = AppSettings.RecentReposComboMinWidth;
        double image = UpstreamImages.Named("DashboardFolderGit")?.Size.Width ?? 32;
        double text = minWidth >= 1
            ? minWidth
            : repositories.Select(repository => new FormattedText(repository.Caption,
                    System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface(FontFamily, FontStyle, FontWeight), FontSize, null).Width)
                .DefaultIfEmpty(0)
                .Max();
        return text + image + TileExtraWidth;
    }

    // Upstream's FormDashboardCategoryTitle: a name is required and must be new.
    async Task<string?> IDashboardWindow.PromptCategoryAsync(IReadOnlyList<string> existing, string? originalName)
    {
        PromptResult? result = await new PromptWindow(
                UpstreamTranslation.ByText(originalName is null ? EnterCaptionText : DashboardViewModel.RenameCategoryText),
                UpstreamTranslation.ByText(CategoryNameText), "OK", initialValue: originalName ?? "", allowEmpty: true)
            .ShowDialog<PromptResult?>(this);
        if (result is null || result.Value == originalName)
        {
            return null;
        }

        if (string.IsNullOrEmpty(result.Value))
        {
            ShowError(UpstreamTranslation.ByText(CategoryNameRequiredText));
            return null;
        }

        if (existing.Contains(result.Value, StringComparer.Ordinal))
        {
            ShowError(UpstreamTranslation.ByText(CategoryNameExistsText));
            return null;
        }

        return result.Value;
    }

    Task<bool> IDashboardWindow.AskAsync(string question, string caption)
        => new ConfirmWindow(question, "Yes", caption, cancelText: "No").ShowDialog<bool>(this);

    // A change of the history shows in the Start menu's recent repositories too.
    async Task IDashboardWindow.RepositoriesChangedAsync()
    {
        _dashboardPaths = [];
        await _recent.LoadAsync();
    }

    void IDashboardWindow.OpenRepository(DashboardRepository repository) => OpenDashboardRepository(repository);

    void IDashboardWindow.OpenFolder(string path) => _fileManager.OpenFolder(path);

    void IDashboardWindow.ShowError(string message) => ShowError(message);

    private void OpenDashboardRepository(DashboardRepository repository)
    {
        if (_git?.Status != GitDiscoveryStatus.NotFound)
        {
            Run(() => OpenRepositoryAsync(repository.Path));
        }
    }

    private IEnumerable<Button> DashboardTiles()
        => DashboardGroups.GetLogicalDescendants().OfType<Button>().Where(button => button.Classes.Contains("dashboardTile"));

    private void OpenUrl(string url) => Run(() => Launcher.LaunchUriAsync(new Uri(url)));

    private static object LinkContent(string? image, string text)
    {
        StackPanel content = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (UpstreamImages.Named(image) is { } picture)
        {
            content.Children.Add(new Image { Source = picture, Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center });
        }

        content.Children.Add(new TextBlock { Text = UpstreamTranslation.ByText(text), VerticalAlignment = VerticalAlignment.Center });
        return content;
    }
}
