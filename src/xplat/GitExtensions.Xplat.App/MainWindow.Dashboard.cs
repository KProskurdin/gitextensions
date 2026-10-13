using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using GitCommands;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

// Upstream's Dashboard and UserRepositoriesList, shown while no repository is open.
public partial class MainWindow
{
    // Upstream's Dashboard links.
    private const string DevelopUrl = "https://github.com/gitextensions/gitextensions";
    private const string DonationUrl = "https://opencollective.com/gitextensions";
    private const string TranslateUrl = "https://github.com/gitextensions/gitextensions/wiki/Translations";
    private const string IssuesUrl = "https://github.com/gitextensions/gitextensions/issues";

    // Upstream's Dashboard, UserRepositoriesList and FormDashboardCategoryTitle texts.
    private const string CloneForkText = "Clone {0} repository";
    private const string ActionsText = "Actions";
    private const string NoCategoryText = "(none)";
    private const string AddCategoryText = "Add new...";
    private const string EnterCaptionText = "Enter Caption";
    private const string CategoryNameText = "Category name";
    private const string CategoryNameRequiredText = "Category name is required";
    private const string CategoryNameExistsText = "Category name already exists";
    private const string RenameCategoryText = "Rename category";
    private const string DeleteCategoryCaption = "Delete Category";
    private const string DeleteCategoryQuestion =
        "Do you want to delete category \"{0}\" with {1} repositories?\n\nThe action cannot be undone.";
    private const string ClearRecentCaption = "Clear recent repositories";
    private const string ClearRecentQuestion =
        "Do you want to clear the list of recent repositories?\n\nThe action cannot be undone.";

    // Upstream's tiles are as wide as the longest caption, its image and 50 pixels, and at least 50 pixels high.
    private const double TileExtraWidth = 50;
    private const double TileMinHeight = 50;

    private DashboardViewModel? _dashboardModel;
    private IReadOnlyList<string> _dashboardPaths = [];

    private DashboardViewModel Dashboard => _dashboardModel ??= new DashboardViewModel(AppServices.RecentRepositories,
        path =>
        {
            bool valid = GitModule.IsValidGitWorkingDir(path);
            return new DashboardRepositoryState(valid,
                valid && AppServices.Preferences.ShowRepoCurrentBranch ? RecentRepositoryBranches.CurrentBranch(path) : null);
        });

    private void WireDashboard()
    {
        foreach (Button link in DashboardStart.Children.Concat(DashboardContribute.Children).OfType<Button>())
        {
            link.Content = LinkContent(link.Tag as string, link.Content as string ?? "");
        }

        DashboardCreateLink.Click += (_, _) => Run(PickAndInitRepositoryAsync);
        DashboardOpenLink.Click += (_, _) => Run(PickAndOpenRepositoryAsync);
        DashboardCloneLink.Click += (_, _) => Run(CloneAsync);
        DashboardDevelopLink.Click += (_, _) => OpenUrl(DevelopUrl);
        DashboardDonateLink.Click += (_, _) => OpenUrl(DonationUrl);
        DashboardTranslateLink.Click += (_, _) => OpenUrl(TranslateUrl);
        DashboardIssuesLink.Click += (_, _) => OpenUrl(IssuesUrl);
        DashboardSearchBox.TextChanged += (_, _) => Dashboard.SearchText = DashboardSearchBox.Text ?? "";

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
                    string.Format(UpstreamTranslation.ByText(CloneForkText), host.Name))
            };
            link.Click += (_, _) => _plugins.StartCloneForkFromHoster(_plugins.Owner, host, gitModuleChanged: null);
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

    private void ShowDashboard()
    {
        DashboardGroups.Children.Clear();
        IReadOnlyList<DashboardGroup> groups = Dashboard.Groups;
        double tileWidth = TileWidth(groups.SelectMany(group => group.Repositories));
        foreach (DashboardGroup group in groups)
        {
            DockPanel header = new() { Margin = new Thickness(0, 4, 0, 2) };
            Button actions = new() { Classes = { "dashboardLink" }, Content = UpstreamTranslation.ByText(ActionsText), Margin = new Thickness(0) };
            actions.Click += (_, _) => ShowCategoryMenu(actions, group);
            DockPanel.SetDock(actions, Dock.Right);
            header.Children.Add(actions);
            header.Children.Add(new TextBlock
            {
                Classes = { "dashboardGroup" },
                Text = group.IsRecent ? UpstreamTranslation.ByText(DashboardViewModel.RecentGroupHeader) : group.Header,
                VerticalAlignment = VerticalAlignment.Center
            });

            WrapPanel tiles = new();
            foreach (DashboardRepository repository in group.Repositories)
            {
                tiles.Children.Add(Tile(repository, tileWidth));
            }

            DashboardGroups.Children.Add(new StackPanel { Name = group.Header, Children = { header, tiles } });
        }
    }

    private Button Tile(DashboardRepository repository, double width)
    {
        Bitmap? folder = UpstreamImages.Named(repository.IsValid ? "DashboardFolderGit" : "DashboardFolderError");
        Panel icon = new() { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 6, 0) };
        icon.Children.Add(new Image { Source = folder, Stretch = Stretch.None });
        if (repository.HasCategory)
        {
            icon.Children.Add(new Image
            {
                Source = UpstreamImages.Named("Star"),
                Width = 16,
                Height = 16,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -6, -6, 0)
            });
        }

        StackPanel texts = new() { Spacing = 1 };
        texts.Children.Add(new TextBlock { Text = repository.Caption, TextTrimming = TextTrimming.CharacterEllipsis });
        if (repository.HasBranch)
        {
            texts.Children.Add(new TextBlock
            {
                Classes = { "branch" },
                Text = repository.Branch,
                FontSize = Math.Max(1, FontSize - 1.33),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        Grid content = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(texts, 1);
        content.Children.Add(icon);
        content.Children.Add(texts);
        Button tile = new()
        {
            Classes = { "dashboardTile" },
            Width = width,
            MinHeight = TileMinHeight,
            Content = content,
            Tag = repository,
            ContextMenu = RepositoryMenu(repository)
        };
        ToolTip.SetTip(tile, repository.Path);
        tile.Click += (_, _) => OpenDashboardRepository(repository);
        return tile;
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

    // Upstream's repository menu: show the folder, put it in a category, remove it or the missing ones.
    private ContextMenu RepositoryMenu(DashboardRepository repository)
    {
        MenuItem showInFolder = new()
        {
            Header = UpstreamTranslation.ByText("Show in folder"),
            Icon = UpstreamIcon("BrowseFileExplorer")
        };
        showInFolder.Click += (_, _) => _fileManager.OpenFolder(repository.Path);
        MenuItem categories = new() { Header = UpstreamTranslation.ByText("Categories") };
        MenuItem remove = new() { Header = UpstreamTranslation.ByText("Remove project from the list") };
        remove.Click += (_, _) => Run(() => ChangeDashboardAsync(() => Dashboard.RemoveAsync(repository)));
        MenuItem removeMissing = new() { Header = UpstreamTranslation.ByText("Remove missing projects from the list") };
        removeMissing.Click += (_, _) => Run(() => ChangeDashboardAsync(Dashboard.RemoveMissingAsync));
        ContextMenu menu = new() { Items = { showInFolder, new Separator(), categories, new Separator(), remove, removeMissing } };
        menu.Opening += (_, _) =>
        {
            removeMissing.IsVisible = Dashboard.HasInvalid;
            categories.ItemsSource = CategoryItems(repository);
        };
        return menu;
    }

    // Upstream's tsmiCategories_DropDownOpening: "(none)" and the categories when there are any, then "Add new..."; the
    // repository's own category and, without one, "(none)" are disabled.
    private List<Control> CategoryItems(DashboardRepository repository)
    {
        List<Control> items = [];
        IReadOnlyList<string> categories = Dashboard.Categories;
        if (categories.Count > 0)
        {
            MenuItem none = new() { Header = UpstreamTranslation.ByText(NoCategoryText), IsEnabled = repository.HasCategory };
            none.Click += (_, _) => Run(() => ChangeDashboardAsync(() => Dashboard.AssignCategoryAsync(repository, null)));
            items.Add(none);
            foreach (string category in categories)
            {
                MenuItem item = new() { Header = category, IsEnabled = category != repository.Category };
                item.Click += (_, _) =>
                    Run(() => ChangeDashboardAsync(() => Dashboard.AssignCategoryAsync(repository, category)));
                items.Add(item);
            }

            items.Add(new Separator());
        }

        MenuItem add = new() { Header = UpstreamTranslation.ByText(AddCategoryText), Icon = UpstreamIcon("BulletAdd") };
        add.Click += (_, _) => Run(async () =>
        {
            if (await PromptCategoryAsync(Dashboard.Categories, originalName: null) is { } name)
            {
                await ChangeDashboardAsync(() => Dashboard.AssignCategoryAsync(repository, name));
            }
        });
        items.Add(add);
        return items;
    }

    // Upstream's group task link: rename or delete a category, or clear the recent repositories.
    private void ShowCategoryMenu(Control target, DashboardGroup group)
    {
        MenuItem rename = new()
        {
            Header = UpstreamTranslation.ByText(RenameCategoryText),
            Icon = UpstreamIcon("FileStatusModified"),
            IsVisible = !group.IsRecent
        };
        rename.Click += (_, _) => Run(async () =>
        {
            if (group.Category is { } category
                && await PromptCategoryAsync([.. Dashboard.Categories.Where(other => other != category)], category) is { } name)
            {
                await ChangeDashboardAsync(() => Dashboard.RenameCategoryAsync(category, name));
            }
        });
        MenuItem delete = new()
        {
            Header = UpstreamTranslation.ByText("Delete category"),
            Icon = UpstreamIcon("StarRemove"),
            IsVisible = !group.IsRecent
        };
        delete.Click += (_, _) => Run(async () =>
        {
            if (group.Category is { } category
                && await AskDashboardAsync(string.Format(UpstreamTranslation.ByText(DeleteCategoryQuestion), category,
                    Dashboard.CountIn(category)), DeleteCategoryCaption))
            {
                await ChangeDashboardAsync(() => Dashboard.RenameCategoryAsync(category, null));
            }
        });
        MenuItem clear = new()
        {
            Header = UpstreamTranslation.ByText("Clear all recent repositories"),
            Icon = UpstreamIcon("CleanupRepo"),
            IsVisible = group.IsRecent
        };
        clear.Click += (_, _) => Run(async () =>
        {
            if (await AskDashboardAsync(UpstreamTranslation.ByText(ClearRecentQuestion), ClearRecentCaption))
            {
                await ChangeDashboardAsync(Dashboard.ClearRecentAsync);
            }
        });
        ContextMenu menu = new() { Items = { rename, delete, clear } };
        target.ContextMenu = menu;
        menu.Open(target);
    }

    // Upstream's FormDashboardCategoryTitle: a name is required and must be new.
    private async Task<string?> PromptCategoryAsync(IReadOnlyList<string> existing, string? originalName)
    {
        PromptResult? result = await new PromptWindow(
                UpstreamTranslation.ByText(originalName is null ? EnterCaptionText : RenameCategoryText),
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

    private Task<bool> AskDashboardAsync(string question, string caption)
        => new ConfirmWindow(question, "Yes", UpstreamTranslation.ByText(caption), cancelText: "No").ShowDialog<bool>(this);

    // A change of the history shows in the Start menu's recent repositories too.
    private async Task ChangeDashboardAsync(Func<Task> change)
    {
        await change();
        _dashboardPaths = [];
        await _recent.LoadAsync();
    }

    private void OpenDashboardRepository(DashboardRepository repository)
    {
        if (_git?.Status != GitDiscoveryStatus.NotFound)
        {
            Run(() => OpenRepositoryAsync(repository.Path));
        }
    }

    private IEnumerable<Button> DashboardTiles()
        => DashboardGroups.Children.OfType<StackPanel>().SelectMany(group => group.Children.OfType<WrapPanel>())
            .SelectMany(tiles => tiles.Children.OfType<Button>());

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
