using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.Plugins;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  What <see cref="SettingsPageTreeViewModel"/> asks of the window: the pages it has and how they look.
/// </summary>
public sealed record SettingsPageTreeServices
{
    /// <summary>
    ///  Upstream's types of the pages the window has; the others are left out of the tree.
    /// </summary>
    public required IReadOnlySet<string> Pages { get; init; }

    /// <summary>
    ///  Upstream's translation of an English text.
    /// </summary>
    public Func<string, string> Translate { get; init; } = text => text;

    /// <summary>
    ///  The picture of a page or a group, as the view shows it.
    /// </summary>
    public Func<SettingsPageEntry, object?> PageImage { get; init; } = _ => null;

    /// <summary>
    ///  The picture of a plugin's page.
    /// </summary>
    public Func<object, object?> PluginImage { get; init; } = _ => null;

    /// <summary>
    ///  The texts the search box looks through on a page (upstream's <c>GetSearchKeywords</c>).
    /// </summary>
    public Func<SettingsPageNode, IEnumerable<string>> PageTexts { get; init; } = _ => [];
}

/// <summary>
///  Upstream's settings tree (<c>FormSettings.OnRuntimeLoad</c>, <c>SettingsTreeViewUserControl</c>): the pages under their
///  groups, the page shown, the search box that marks the pages it finds, and the window title naming the page.
/// </summary>
public sealed partial class SettingsPageTreeViewModel : ObservableObject
{
    /// <summary>
    ///  The page type of a plugin's page; its nodes also carry the plugin.
    /// </summary>
    public const string PluginPage = "PluginSettingsPage";

    public const string ChecklistPage = "ChecklistSettingsPage";
    public const string PluginRootPage = "PluginRootIntroductionPage";

    private const string WindowTitle = "Settings";
    private const string PluginTitlePrefix = "Plugin: ";

    // As upstream's _lastSelectedSettingsPageType: the next settings window of this session opens on the page shown last.
    private static string? _lastPage;

    private readonly SettingsPageTreeServices _services;
    private readonly List<SettingsPageNode> _allNodes = [];
    private readonly List<SettingsPageNode> _foundNodes = [];

    /// <param name="services">The window's pages.</param>
    /// <param name="pluginPages">The plugins' pages, in the Plugins group: <c>PluginOption</c>s and <c>SkippedPluginOption</c>s.</param>
    public SettingsPageTreeViewModel(SettingsPageTreeServices services, IEnumerable<object> pluginPages)
    {
        _services = services;
        Dictionary<string, SettingsPageNode> nodes = [];
        List<SettingsPageNode> roots = [];
        foreach (SettingsPageEntry entry in SettingsPageTree.Entries)
        {
            bool ported = services.Pages.Contains(entry.Page);
            if (entry.Parent is null)
            {
                SettingsPageNode group = new(entry.Page, services.Translate(entry.Title), services.PageImage(entry),
                    page: null);
                roots.Add(group);
                nodes[entry.Page] = group;
            }
            else if (!ported)
            {
                continue;
            }
            else if (entry.AsRoot)
            {
                nodes[entry.Parent].Page = entry.Page;
                nodes[entry.Parent].PageTitle = services.Translate(entry.Title);
            }
            else
            {
                SettingsPageNode node = new(entry.Page, services.Translate(entry.Title), services.PageImage(entry),
                    entry.Page);
                nodes[entry.Parent].Add(node);
                nodes[entry.Page] = node;
            }
        }

        foreach (object plugin in pluginPages)
        {
            nodes[SettingsPageTree.PluginsGroup].Add(new SettingsPageNode($"{PluginPage}:{plugin}", plugin.ToString()!,
                services.PluginImage(plugin), PluginPage, plugin));
        }

        Roots = roots;
        AddAll(roots);
        SearchPlaceholder = services.Translate(SettingsPageTree.TypeToFind);
        return;

        void AddAll(IEnumerable<SettingsPageNode> level)
        {
            foreach (SettingsPageNode node in level)
            {
                _allNodes.Add(node);
                AddAll(node.Children);
            }
        }
    }

    /// <summary>
    ///  The root nodes: upstream's Git Extensions, Git and Plugins groups.
    /// </summary>
    public IReadOnlyList<SettingsPageNode> Roots { get; }

    public string SearchPlaceholder { get; }

    /// <summary>
    ///  The node chosen in the tree.
    /// </summary>
    [ObservableProperty]
    public partial SettingsPageNode? SelectedNode { get; set; }

    /// <summary>
    ///  The node whose page is shown: it has a page, unlike a group without one.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInstantSavePage))]
    public partial SettingsPageNode? ShownNode { get; private set; }

    /// <summary>
    ///  The window title, naming the page shown.
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    /// <summary>
    ///  True on the pages that save at once (the Checklist), where upstream notes that Cancel does not revert them.
    /// </summary>
    public bool IsInstantSavePage => ShownNode?.Page == ChecklistPage;

    [ObservableProperty]
    public partial string? SearchText { get; set; }

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
        SelectedNode = node;
        ShowPage(node);
    }

    /// <summary>
    ///  Shows the page of upstream's page type <paramref name="page"/> (such as <c>GitConfigSettingsPage</c>), as
    ///  upstream's settings dialog started with a page reference.
    /// </summary>
    public void GoToPage(string page) => GoToPage(_allNodes.First(node => node.Page == page && node.Plugin is null));

    /// <summary>
    ///  Shows the first page: a plugin's page when <paramref name="pluginsPage"/> (<paramref name="plugin"/>'s, or the
    ///  first), otherwise the page shown last in this session, or the first group's.
    /// </summary>
    public void GoToInitialPage(bool pluginsPage, IGitPlugin? plugin)
    {
        IEnumerable<SettingsPageNode> pluginNodes = _allNodes.Where(node => node.Plugin is not null);
        SettingsPageNode? initial = pluginsPage
            ? (plugin is null
                  ? pluginNodes.FirstOrDefault()
                  : pluginNodes.FirstOrDefault(node => node.Plugin is PluginOption option && option.Plugin == plugin))
              ?? _allNodes.First(node => node.Page == PluginRootPage)
            : _allNodes.FirstOrDefault(node => node.Key == _lastPage);
        GoToPage(initial ?? Roots[0]);
    }

    // Upstream's group nodes without a page of their own show their first page.
    private void ShowPage(SettingsPageNode node)
    {
        if (node.Page is null)
        {
            if (node.Children.Count > 0)
            {
                GoToPage(node.Children[0]);
            }

            return;
        }

        // Selecting the node in the tree shows the page already.
        if (node == ShownNode)
        {
            return;
        }

        _lastPage = node.Key;
        Title = $"{_services.Translate(WindowTitle)} - {(node.Plugin is null ? "" : PluginTitlePrefix)}{node.PageTitle}";
        ShownNode = node;
    }

    partial void OnSelectedNodeChanged(SettingsPageNode? value)
    {
        if (value is not null)
        {
            ShowPage(value);
        }
    }

    // Upstream's textBoxFind_TextChanged: marks every page whose title or texts match, and shows them.
    partial void OnSearchTextChanged(string? value)
    {
        _foundNodes.Clear();
        foreach (SettingsPageNode node in _allNodes)
        {
            node.IsFound = SettingsPageTree.Matches(value ?? "", node.Title,
                node.Keywords ??= [.. _services.PageTexts(node)]);
            if (node.IsFound)
            {
                _foundNodes.Add(node);
                for (SettingsPageNode? parent = node.Parent; parent is not null; parent = parent.Parent)
                {
                    parent.IsExpanded = true;
                }
            }
        }
    }

    // Upstream's textBoxFind_KeyUp: each Enter shows the next page found, from the first again after the last.
    [RelayCommand]
    private void FindNext()
    {
        if (_foundNodes.Count == 0)
        {
            return;
        }

        int index = SelectedNode is { } selected ? _foundNodes.IndexOf(selected) : -1;
        GoToPage(_foundNodes[index == -1 || index + 1 == _foundNodes.Count ? 0 : index + 1]);
    }
}
