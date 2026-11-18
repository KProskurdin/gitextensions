using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;
using ToolStripItem = System.Windows.Forms.ToolStripItem;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace GitExtensions.Xplat.Core.Plugins;

/// <summary>
///  A plugin's WinForms menu items as entries (MVVM.md): the items a repository host plugin adds to a menu through
///  <c>IRepositoryHostPlugin.ConfigureContextMenu</c>, such as the GitHub plugin's "View in GitHub". An entry runs the
///  plugin's item, as a click on it would.
/// </summary>
public static class PluginMenuEntries
{
    /// <summary>
    ///  Runs the plugin's item an entry stands for.
    /// </summary>
    public static IRelayCommand<ToolStripItem> PerformClickCommand { get; } =
        new RelayCommand<ToolStripItem>(item => item?.PerformClick());

    /// <summary>
    ///  <paramref name="items"/> as entries, with their submenus; the texts as they are (a remote's name may have
    ///  underscores, which are not access keys here).
    /// </summary>
    public static IReadOnlyList<MenuEntry> From(IEnumerable<ToolStripItem> items) => [.. items.Select(Entry)];

    private static MenuEntry Entry(ToolStripItem item)
        => item is ToolStripMenuItem { DropDownItems.Count: > 0 } parent
            ? new MenuEntry(item.Text ?? "") { Children = From(parent.DropDownItems.Cast<ToolStripItem>()) }
            : new MenuEntry(item.Text ?? "", PerformClickCommand, item);
}
