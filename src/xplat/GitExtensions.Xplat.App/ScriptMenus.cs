using System.Windows.Input;
using Avalonia.Controls;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Scripts;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>UserScriptContextMenuExtensions.AddUserScripts</c>: puts every enabled script in a context menu. The scripts
///  <c>addDirect</c> selects become items of the menu itself, right after the host item ("Run script"); the others are listed
///  under the host item, which is enabled only when it lists any. Each item has the script's icon and hotkey.
/// </summary>
internal static class ScriptMenus
{
    // Marks the items this class added to a menu, as upstream's "_ownScript" name suffix does, so they can be removed again.
    private const string ScriptItemClass = "ownScript";

    private const double IconSize = 16;

    /// <summary>
    ///  Replaces the script items of <paramref name="menu"/> and <paramref name="host"/> with the current scripts; true when
    ///  any were added. Call it when the menu opens, so changed scripts show up.
    /// </summary>
    /// <param name="run">A view model's command that runs the script it is given.</param>
    public static bool Fill(ContextMenu menu, MenuItem host, Func<ScriptDefinition, bool> addDirect, ICommand run)
    {
        Remove(menu, host);
        int index = menu.Items.IndexOf(host);
        bool added = false;
        foreach (ScriptDefinition script in AppServices.Scripts.Load().Where(script => script.Enabled))
        {
            MenuItem item = Create(script, run, menu);
            if (addDirect(script) && index >= 0)
            {
                item.Classes.Add(ScriptItemClass);
                menu.Items.Insert(++index, item);
            }
            else
            {
                host.Items.Add(item);
                host.IsEnabled = true;
            }

            added = true;
        }

        return added;
    }

    /// <summary>
    ///  Takes the scripts out of <paramref name="menu"/> again (upstream's <c>RemoveUserScripts</c>).
    /// </summary>
    public static void Remove(ContextMenu menu, MenuItem host)
    {
        host.ItemsSource = null;
        host.Items.Clear();
        host.IsEnabled = false;
        foreach (MenuItem item in menu.Items.OfType<MenuItem>().Where(item => item.Classes.Contains(ScriptItemClass)).ToList())
        {
            menu.Items.Remove(item);
        }
    }

    // The name as it is: an & in it is upstream's access key, which Avalonia does not read; DisplayName drops it.
    private static MenuItem Create(ScriptDefinition script, ICommand run, ContextMenu menu)
    {
        MenuItem item = (MenuItem)MenuEntries.Item(new MenuEntry(script.DisplayName, run, script), menu);
        item.InputGesture = Hotkeys.Scripts.GestureFor(script.HotkeyCommandIdentifier);
        if (ScriptIcons.For(script) is { } icon)
        {
            item.Icon = new Image { Source = icon, Width = IconSize, Height = IconSize };
        }

        return item;
    }
}
