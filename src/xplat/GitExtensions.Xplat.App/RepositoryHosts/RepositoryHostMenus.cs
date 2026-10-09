using Avalonia.Controls;
using GitExtensions.Extensibility.Plugins;
using GitUIPluginInterfaces.RepositoryHosts;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using ToolStripItem = System.Windows.Forms.ToolStripItem;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace GitExtensions.Xplat.App.RepositoryHosts;

/// <summary>
///  The items a repository host plugin adds to upstream's blame context menu (<c>BlameControl</c>), such as the GitHub
///  plugin's "View in GitHub": the plugin fills a WinForms menu through <see cref="IRepositoryHostPlugin.ConfigureContextMenu"/>
///  with the blamed line as its tag, as upstream sets it, and its items become Avalonia menu items that click the plugin's.
/// </summary>
public static class RepositoryHostMenus
{
    public static IReadOnlyList<MenuItem> ForBlame(IRepositoryHostPlugin host, GitBlameContext blameContext)
    {
        ContextMenuStrip menu = new() { Tag = blameContext };
        host.ConfigureContextMenu(menu);
        return [.. menu.Items.Select(ToMenuItem)];
    }

    private static MenuItem ToMenuItem(ToolStripItem item)
    {
        // The text as it is: a remote's name may have underscores, which are not access keys here.
        MenuItem menuItem = new() { Header = new TextBlock { Text = item.Text } };
        if (item is ToolStripMenuItem { DropDownItems.Count: > 0 } parent)
        {
            foreach (ToolStripItem child in parent.DropDownItems)
            {
                menuItem.Items.Add(ToMenuItem(child));
            }
        }
        else
        {
            menuItem.Click += (_, _) => item.PerformClick();
        }

        return menuItem;
    }
}
