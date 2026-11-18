using Avalonia;
using Avalonia.Controls;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.Plugins;
using GitUIPluginInterfaces.RepositoryHosts;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using ToolStripItem = System.Windows.Forms.ToolStripItem;

namespace GitExtensions.Xplat.App.RepositoryHosts;

/// <summary>
///  The items a repository host plugin adds to upstream's blame context menu (<c>BlameControl</c>), such as the GitHub
///  plugin's "View in GitHub": the plugin fills a WinForms menu through <see cref="IRepositoryHostPlugin.ConfigureContextMenu"/>
///  with the blamed line as its tag, as upstream sets it, and its items become entries (<see cref="PluginMenuEntries"/>)
///  that run the plugin's.
/// </summary>
public static class RepositoryHostMenus
{
    public static IReadOnlyList<MenuItem> ForBlame(IRepositoryHostPlugin host, GitBlameContext blameContext)
    {
        ContextMenuStrip menu = new() { Tag = blameContext };
        host.ConfigureContextMenu(menu);
        return [.. PluginMenuEntries.From(menu.Items.Cast<ToolStripItem>())
            .Select(entry => (MenuItem)MenuEntries.Item(entry, Application.Current!))];
    }
}
