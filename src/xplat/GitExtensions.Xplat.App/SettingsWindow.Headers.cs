using Avalonia.Controls;
using GitExtensions.Extensibility.Settings;

namespace GitExtensions.Xplat.App;

// Upstream's SettingsPageWithHeader: every page but the groups' introductions shows the settings source it edits. The
// repository's settings pages (upstream's DistributedSettingsPage) offer its levels while a repository is open; the
// others edit the user's settings.
public partial class SettingsWindow
{
    private readonly Dictionary<TabItem, SettingsSourceHeader> _headers = [];

    /// <summary>
    ///  The "Settings source" header of a page.
    /// </summary>
    public SettingsSourceHeader SourceHeaderOf(TabItem page) => _headers[page];

    private void AddPageHeaders()
    {
        TabItem[] withoutHeader = [GitRootTab, PluginsRootTab];
        TabItem[] repositoryPages = [RevisionLinksTab, BuildServerTab, DetailedTab, PluginsTab];
        IReadOnlyList<SettingLevel> repositoryLevels = _repositoryPath is null
            ? [SettingLevel.Global]
            : [SettingLevel.Effective, SettingLevel.Local, SettingLevel.Distributed, SettingLevel.Global];

        // Upstream's git config pages (IGitConfigSettingsPage) offer the system level instead of the distributed one.
        IReadOnlyList<SettingLevel> gitConfigLevels = _repositoryPath is null
            ? [SettingLevel.Global]
            : [SettingLevel.Effective, SettingLevel.Local, SettingLevel.Global, SettingLevel.SystemWide];
        foreach (TabItem page in Tabs.Items.OfType<TabItem>().Except(withoutHeader))
        {
            SettingsSourceHeader header = new(page == GitConfigTab ? gitConfigLevels
                : repositoryPages.Contains(page) ? repositoryLevels
                : [SettingLevel.Global]);
            Control body = (Control)page.Content!;
            page.Content = null;

            // As upstream's pages, a page taller than the window scrolls; the pages with lists scroll those themselves.
            if (body is StackPanel)
            {
                body = new ScrollViewer { Content = body };
            }

            DockPanel.SetDock(header, Dock.Top);
            page.Content = new DockPanel { Children = { header, body } };
            _headers[page] = header;
            ShowReadOnly();
            header.LevelChanged += (_, _) =>
            {
                ShowReadOnly();
                if (page == RevisionLinksTab)
                {
                    ChangeRevisionLinkLevel(header.Level);
                }
                else if (page == BuildServerTab)
                {
                    ChangeBuildServerLevel(header.Level);
                }
                else if (page == PluginsTab)
                {
                    ChangePluginLevel(header.Level);
                }
                else if (page == GitConfigTab)
                {
                    ChangeGitConfigLevel(header.Level);
                }
            };

            continue;

            // Upstream's header disables the page while it shows read-only settings; the Detailed page's revision graph
            // settings are the user's, so they are edited at the global level only.
            void ShowReadOnly()
                => body.IsEnabled = !header.IsReadOnly && (page != DetailedTab || header.Level == SettingLevel.Global);
        }

        _buildServerLevel = _headers[BuildServerTab].Level;
        _pluginLevel = _headers[PluginsTab].Level;
    }
}
