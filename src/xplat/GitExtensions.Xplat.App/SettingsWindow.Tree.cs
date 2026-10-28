using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.Plugins;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

// Upstream's settings tree (FormSettings.OnRuntimeLoad, SettingsTreeViewUserControl): the pages under their groups with
// upstream's images, the search box that marks the pages it finds, and the window title naming the page.
public partial class SettingsWindow
{
    private const string WindowTitle = "Settings";
    private const string PluginTitlePrefix = "Plugin: ";

    // As upstream's _lastSelectedSettingsPageType: the next settings window of this session opens on the page shown last.
    private static string? _lastPage;

    private readonly List<SettingsPageNode> _allPageNodes = [];
    private readonly List<SettingsPageNode> _foundPageNodes = [];
    private bool _showingPage;
    private SettingsPageNode? _shownPage;

    /// <summary>
    ///  The page tree's root nodes: upstream's Git Extensions, Git and Plugins groups.
    /// </summary>
    public IReadOnlyList<SettingsPageNode> PageTree { get; private set; } = [];

    /// <summary>
    ///  The node of the page shown, or null before one is.
    /// </summary>
    public SettingsPageNode? SelectedPage => SettingsTree.SelectedItem as SettingsPageNode;

    /// <summary>
    ///  Shows the page of <paramref name="node"/> as upstream's tree does when a node is chosen: a group without a page of
    ///  its own shows its first page.
    /// </summary>
    public void GoToPage(SettingsPageNode node)
    {
        for (SettingsPageNode? parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            parent.IsExpanded = true;
        }

        node.IsExpanded = true;
        SettingsTree.SelectedItem = node;
        ShowPage(node);
    }

    /// <summary>
    ///  Shows the page of upstream's page type <paramref name="upstreamPage"/> (such as <c>GitConfigSettingsPage</c>), as
    ///  upstream's settings dialog started with a page reference.
    /// </summary>
    public void GoToPage(string upstreamPage)
    {
        TabItem page = PortedPages()[upstreamPage];
        GoToPage(_allPageNodes.First(node => node.Page == page));
    }

    // The pages the new shell has, by upstream's page type; the others are left out of the tree.
    private Dictionary<string, TabItem> PortedPages() => new()
    {
        ["ChecklistSettingsPage"] = ChecklistTab,
        ["GeneralSettingsPage"] = GeneralTab,
        ["AppearanceSettingsPage"] = AppearanceTab,
        ["SortingSettingsPage"] = SortingTab,
        ["ColorsSettingsPage"] = ColorsTab,
        ["AppearanceFontsSettingsPage"] = FontsTab,
        ["RevisionLinksSettingsPage"] = RevisionLinksTab,
        ["BuildServerIntegrationSettingsPage"] = BuildServerTab,
        ["ScriptsSettingsPage"] = ScriptsTab,
        ["HotkeysSettingsPage"] = HotkeysTab,
        ["AdvancedSettingsPage"] = AdvancedTab,
        ["ConfirmationsSettingsPage"] = ConfirmationsTab,
        ["DetailedSettingsPage"] = DetailedTab,
        ["FormBrowseRepoSettingsPage"] = BrowseRepositoryTab,
        ["CommitDialogSettingsPage"] = CommitDialogTab,
        ["DiffViewerSettingsPage"] = DiffViewerTab,
        ["BlameViewerSettingsPage"] = BlameViewerTab,
        ["SshSettingsPage"] = SshTab,
        ["GitSettingsPage"] = GitTab,
        ["GitConfigSettingsPage"] = GitConfigTab,
        ["GitRootIntroductionPage"] = GitRootTab,
        ["PluginRootIntroductionPage"] = PluginsRootTab,
    };

    // initialPage: a plugin, PluginsTab for the first plugin, or null for the page shown last.
    private void ShowPageTree(object? initialPage)
    {
        Dictionary<string, TabItem> pages = PortedPages();
        Dictionary<string, SettingsPageNode> nodes = [];
        List<SettingsPageNode> roots = [];
        foreach (SettingsPageEntry entry in SettingsPageTree.Entries)
        {
            TabItem? page = pages.GetValueOrDefault(entry.Page);
            if (entry.Parent is null)
            {
                SettingsPageNode group = new(entry.Page, UpstreamTranslation.ByText(entry.Title),
                    UpstreamImages.Named(entry.Image), page: null);
                roots.Add(group);
                nodes[entry.Page] = group;
            }
            else if (page is null)
            {
                continue;
            }
            else if (entry.AsRoot)
            {
                nodes[entry.Parent].Page = page;
                nodes[entry.Parent].PageTitle = UpstreamTranslation.ByText(entry.Title);
            }
            else
            {
                SettingsPageNode node = new(page.Name!, UpstreamTranslation.ByText(entry.Title),
                    UpstreamImages.Named(entry.Image), page);
                nodes[entry.Parent].Add(node);
                nodes[entry.Page] = node;
            }
        }

        foreach (object plugin in PluginPages())
        {
            nodes[SettingsPageTree.PluginsGroup].Add(new SettingsPageNode(PluginsTab.Name + ":" + plugin,
                plugin.ToString()!,
                plugin is PluginOption { Plugin: var loaded } ? PluginHost.LoadIcon(loaded) : null, PluginsTab,
                plugin));
        }

        PageTree = roots;
        AddAll(roots);
        SettingsTree.ItemsSource = roots;
        SettingsTree.SelectionChanged += (_, _) =>
        {
            if (SettingsTree.SelectedItem is SettingsPageNode node)
            {
                ShowPage(node);
            }
        };

        // Code that shows a page by its tab (a page with an error on save) selects its node.
        Tabs.SelectionChanged += (_, e) =>
        {
            if (e.Source == Tabs && !_showingPage && Tabs.SelectedItem is TabItem tab &&
                _allPageNodes.FirstOrDefault(node => node.Page == tab) is { } node)
            {
                GoToPage(node);
            }
        };
        SettingsSearchBox.PlaceholderText = UpstreamTranslation.ByText(SettingsPageTree.TypeToFind);

        // Not TextChanged: it is raised later, so Enter pressed right after typing would find nothing yet.
        SettingsSearchBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                FindPages(SettingsSearchBox.Text);
            }
        };
        SettingsSearchBox.KeyUp += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SelectNextFoundPage();
            }
        };

        GoToPage(InitialPage(initialPage) ?? roots[0]);
        return;

        void AddAll(IEnumerable<SettingsPageNode> level)
        {
            foreach (SettingsPageNode node in level)
            {
                _allPageNodes.Add(node);
                AddAll(node.Children);
            }
        }
    }

    private SettingsPageNode? InitialPage(object? initialPage)
    {
        IEnumerable<SettingsPageNode> pluginPages = _allPageNodes.Where(node => node.Plugin is not null);
        return initialPage switch
        {
            IGitPlugin plugin => pluginPages.FirstOrDefault(node => node.Plugin is PluginOption option
                                                                    && option.Plugin == plugin)
                                 ?? _allPageNodes.First(node => node.Page == PluginsRootTab),
            TabItem => pluginPages.FirstOrDefault() ?? _allPageNodes.First(node => node.Page == PluginsRootTab),
            _ => _allPageNodes.FirstOrDefault(node => node.Key == _lastPage),
        };
    }

    private void ShowPage(SettingsPageNode node)
    {
        // Upstream's group nodes without a page of their own show their first page.
        if (node.Page is null)
        {
            if (node.Children.Count > 0)
            {
                GoToPage(node.Children[0]);
            }

            return;
        }

        // Selecting the node in the tree shows the page already.
        if (node == _shownPage)
        {
            return;
        }

        _shownPage = node;
        if (node.Plugin is { } plugin)
        {
            ShowPlugin(plugin);
        }

        _showingPage = true;
        Tabs.SelectedItem = node.Page;
        _showingPage = false;
        _lastPage = node.Key;
        OnPageShown(node.Page);
        Title =
            $"{UpstreamTranslation.ByText(WindowTitle)} - {(node.Plugin is null ? "" : PluginTitlePrefix)}{node.PageTitle}";
    }

    // Upstream's textBoxFind_TextChanged: marks every page whose title or texts match, and shows them.
    private void FindPages(string? search)
    {
        _foundPageNodes.Clear();
        foreach (SettingsPageNode node in _allPageNodes)
        {
            node.IsFound = SettingsPageTree.Matches(search ?? "", node.Title, node.Keywords ??= [.. PageTexts(node)]);
            if (node.IsFound)
            {
                _foundPageNodes.Add(node);
                for (SettingsPageNode? parent = node.Parent; parent is not null; parent = parent.Parent)
                {
                    parent.IsExpanded = true;
                }
            }
        }
    }

    // Upstream's textBoxFind_KeyUp: each Enter shows the next page found, from the first again after the last.
    private void SelectNextFoundPage()
    {
        if (_foundPageNodes.Count == 0)
        {
            return;
        }

        int index = SelectedPage is { } selected ? _foundPageNodes.IndexOf(selected) : -1;
        GoToPage(_foundPageNodes[index == -1 || index + 1 == _foundPageNodes.Count ? 0 : index + 1]);
    }

    // Upstream's GetSearchKeywords: the texts of the page's visible controls; a plugin's page, its settings' captions.
    private IEnumerable<string> PageTexts(SettingsPageNode node)
        => node.Plugin switch
        {
            PluginOption { Plugin: var plugin } => PluginEditor(plugin).Rows
                .SelectMany(row => (string?[])[row.Caption, row.Text]).OfType<string>(),
            SkippedPluginOption { File: var file } => [file.FileName, file.Reason],
            _ => node.Page?.Content is ILogical content ? ControlTexts(content) : [],
        };

    private static IEnumerable<string> ControlTexts(ILogical root)
    {
        // Upstream passes over hidden controls with their children, and number boxes.
        if (root is Control { IsVisible: false } or NumericUpDown)
        {
            yield break;
        }

        if (root is Control { IsEnabled: true } control && ControlText(control) is { } text &&
            !string.IsNullOrWhiteSpace(text))
        {
            yield return text.Trim();
        }

        foreach (ILogical child in root.LogicalChildren)
        {
            foreach (string childText in ControlTexts(child))
            {
                yield return childText;
            }
        }
    }

    private static string? ControlText(Control control) => control switch
    {
        TextBlock textBlock => textBlock.Text,
        TextBox { PasswordChar: '\0' } textBox => textBox.Text,
        ComboBox comboBox => comboBox.SelectedItem?.ToString(),
        HeaderedContentControl { Header: string header } => header,
        ContentControl { Content: string content } => content,
        _ => null,
    };
}

/// <summary>
///  A node of the settings window's page tree: one of upstream's groups, a page, or a plugin's page.
/// </summary>
public sealed class SettingsPageNode(string key, string title, Bitmap? icon, TabItem? page, object? plugin = null)
    : INotifyPropertyChanged
{
    private readonly List<SettingsPageNode> _children = [];
    private bool _isExpanded;
    private bool _isFound;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///  Names the page across windows: its tab's name, or the plugin's.
    /// </summary>
    public string Key { get; } = key;

    public string Title { get; } = title;

    public Bitmap? Icon { get; } = icon;

    /// <summary>
    ///  The page shown for the node; null for a group that shows its first page.
    /// </summary>
    public TabItem? Page { get; internal set; } = page;

    /// <summary>
    ///  The title of the page shown, for the window title: a group's own page has its own title.
    /// </summary>
    public string PageTitle { get; internal set; } = title;

    /// <summary>
    ///  The plugin of a plugin's page: a <see cref="PluginOption"/> or a <see cref="SkippedPluginOption"/>.
    /// </summary>
    public object? Plugin { get; } = plugin;

    public SettingsPageNode? Parent { get; private set; }

    public IReadOnlyList<SettingsPageNode> Children => _children;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value, nameof(IsExpanded));
    }

    /// <summary>
    ///  True while the search box finds the page.
    /// </summary>
    public bool IsFound
    {
        get => _isFound;
        set => Set(ref _isFound, value, nameof(IsFound));
    }

    // The page's texts, read once, at the first search, as upstream does.
    internal IReadOnlyList<string>? Keywords { get; set; }

    internal void Add(SettingsPageNode child)
    {
        child.Parent = this;
        _children.Add(child);
    }

    public override string ToString() => Title;

    private void Set(ref bool field, bool value, string name)
    {
        if (field != value)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
