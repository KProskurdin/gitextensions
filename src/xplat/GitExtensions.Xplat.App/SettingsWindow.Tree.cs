using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using GitExtensions.Xplat.Core.Plugins;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

// Upstream's settings tree (FormSettings.OnRuntimeLoad, SettingsTreeViewUserControl), whose state is the view model's
// SettingsPageTreeViewModel: here the pages' tabs, the pictures, the controls' texts the search looks through, and Enter
// in the search box.
public partial class SettingsWindow
{
    // The tabs of the pages the new shell has, by upstream's page type; the others are left out of the tree.
    private static readonly Dictionary<string, string> _pageTabs = new()
    {
        ["ChecklistSettingsPage"] = nameof(ChecklistTab),
        ["GeneralSettingsPage"] = nameof(GeneralTab),
        ["AppearanceSettingsPage"] = nameof(AppearanceTab),
        ["SortingSettingsPage"] = nameof(SortingTab),
        ["ColorsSettingsPage"] = nameof(ColorsTab),
        ["AppearanceFontsSettingsPage"] = nameof(FontsTab),
        ["RevisionLinksSettingsPage"] = nameof(RevisionLinksTab),
        ["BuildServerIntegrationSettingsPage"] = nameof(BuildServerTab),
        ["ScriptsSettingsPage"] = nameof(ScriptsTab),
        ["HotkeysSettingsPage"] = nameof(HotkeysTab),
        [ShellExtensionPage] = nameof(ShellExtensionTab),
        ["AdvancedSettingsPage"] = nameof(AdvancedTab),
        ["ConfirmationsSettingsPage"] = nameof(ConfirmationsTab),
        ["DetailedSettingsPage"] = nameof(DetailedTab),
        ["FormBrowseRepoSettingsPage"] = nameof(BrowseRepositoryTab),
        ["CommitDialogSettingsPage"] = nameof(CommitDialogTab),
        ["DiffViewerSettingsPage"] = nameof(DiffViewerTab),
        ["BlameViewerSettingsPage"] = nameof(BlameViewerTab),
        ["SshSettingsPage"] = nameof(SshTab),
        ["GitSettingsPage"] = nameof(GitTab),
        ["GitConfigSettingsPage"] = nameof(GitConfigTab),
        ["GitConfigAdvancedSettingsPage"] = nameof(GitConfigAdvancedTab),
        ["GitRootIntroductionPage"] = nameof(GitRootTab),
        ["PluginRootIntroductionPage"] = nameof(PluginsRootTab),
        [SettingsPageTreeViewModel.PluginPage] = nameof(PluginsTab),
    };

    private bool _showingPage;

    /// <summary>
    ///  The page tree's root nodes: upstream's Git Extensions, Git and Plugins groups.
    /// </summary>
    public IReadOnlyList<SettingsPageNode> PageTree => ViewModel.PageTree.Roots;

    /// <summary>
    ///  The node of the page shown, or null before one is.
    /// </summary>
    public SettingsPageNode? SelectedPage => ViewModel.PageTree.SelectedNode;

    /// <summary>
    ///  Shows the page of <paramref name="node"/> as upstream's tree does when a node is chosen: a group without a page of
    ///  its own shows its first page.
    /// </summary>
    public void GoToPage(SettingsPageNode node) => ViewModel.PageTree.GoToPage(node);

    /// <summary>
    ///  Shows the page of upstream's page type <paramref name="upstreamPage"/> (such as <c>GitConfigSettingsPage</c>), as
    ///  upstream's settings dialog started with a page reference.
    /// </summary>
    public void GoToPage(string upstreamPage) => ViewModel.PageTree.GoToPage(upstreamPage);

    // Upstream's Shell extension page: the Explorer shell extension is Windows'.
    private const string ShellExtensionPage = "ShellExtensionSettingsPage";

    private static SettingsPageTreeServices PageTreeServices(Func<SettingsPageNode, IEnumerable<string>> pageTexts)
        => new()
        {
            Pages = _pageTabs.Keys.Where(page => page != ShellExtensionPage || OperatingSystem.IsWindows()).ToHashSet(),
            Translate = UpstreamTranslation.ByText,
            PageImage = PageImage,
            PluginImage = plugin => plugin is PluginOption { Plugin: var loaded } ? PluginHost.LoadIcon(loaded) : null,
            PageTexts = pageTexts,
        };

    // initialPage: as the window's constructor takes it.
    private void ShowPageTree(bool pluginsPage, GitExtensions.Extensibility.Plugins.IGitPlugin? selectedPlugin)
    {
        ViewModel.PageTree.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsPageTreeViewModel.ShownNode) &&
                ViewModel.PageTree.ShownNode is { } node)
            {
                _showingPage = true;
                Tabs.SelectedItem = TabOf(node);
                _showingPage = false;
            }
        };

        // Code that shows a page by its tab selects its node.
        Tabs.SelectionChanged += (_, e) =>
        {
            if (e.Source == Tabs && !_showingPage && Tabs.SelectedItem is TabItem { Name: { } tab } &&
                _pageTabs.FirstOrDefault(entry => entry.Value == tab).Key is { } page &&
                page != SettingsPageTreeViewModel.PluginPage)
            {
                GoToPage(page);
            }
        };

        // Upstream's textBoxFind_KeyUp: each Enter shows the next page found.
        SettingsSearchBox.KeyUp += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ViewModel.PageTree.FindNextCommand.Execute(null);
            }
        };

        ViewModel.PageTree.GoToInitialPage(pluginsPage, selectedPlugin);
    }

    private TabItem? TabOf(SettingsPageNode node)
        => node.Page is { } page && _pageTabs.TryGetValue(page, out string? tab)
            ? this.FindControl<TabItem>(tab)
            : null;

    // Upstream's GetSearchKeywords for a page of the window: the texts of its visible controls.
    private IEnumerable<string> PageTexts(SettingsPageNode node)
        => TabOf(node)?.Content is ILogical content ? ControlTexts(content) : [];

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

    private static Bitmap? PageImage(SettingsPageEntry entry)
        => entry.AdaptLightness
            ? ThemedImages.AdaptLightness(UpstreamImages.Named(entry.Image))
            : UpstreamImages.Named(entry.Image);
}
