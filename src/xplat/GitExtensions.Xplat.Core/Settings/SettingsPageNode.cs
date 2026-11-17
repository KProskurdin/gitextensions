using CommunityToolkit.Mvvm.ComponentModel;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  A node of the settings window's page tree: one of upstream's groups, a page, or a plugin's page.
/// </summary>
/// <param name="key">Names the node across windows: upstream's page type, or the plugin page's and the plugin's.</param>
/// <param name="title">The node's text.</param>
/// <param name="icon">The node's picture, as the view made it.</param>
/// <param name="page">Upstream's type of the page shown; null for a group that shows its first page.</param>
/// <param name="plugin">The plugin of a plugin's page.</param>
public sealed partial class SettingsPageNode(
    string key,
    string title,
    object? icon,
    string? page,
    object? plugin = null)
    : ObservableObject
{
    private readonly List<SettingsPageNode> _children = [];

    public string Key { get; } = key;

    public string Title { get; } = title;

    public object? Icon { get; } = icon;

    /// <summary>
    ///  Upstream's type of the page shown for the node (such as <c>GitConfigSettingsPage</c>); null for a group that shows
    ///  its first page.
    /// </summary>
    public string? Page { get; internal set; } = page;

    /// <summary>
    ///  The title of the page shown, for the window title: a group's own page has its own title.
    /// </summary>
    public string PageTitle { get; internal set; } = title;

    /// <summary>
    ///  The plugin of a plugin's page: a <c>PluginOption</c> or a <c>SkippedPluginOption</c>.
    /// </summary>
    public object? Plugin { get; } = plugin;

    public SettingsPageNode? Parent { get; private set; }

    public IReadOnlyList<SettingsPageNode> Children => _children;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>
    ///  True while the search box finds the page.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFound { get; set; }

    // The page's texts, read once, at the first search, as upstream does.
    internal IReadOnlyList<string>? Keywords { get; set; }

    internal void Add(SettingsPageNode child)
    {
        child.Parent = this;
        _children.Add(child);
    }

    public override string ToString() => Title;
}
