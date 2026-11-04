using Avalonia.Controls;
using Avalonia.Media;
using GitExtensions.Xplat.Core.Settings;
using ResourceManager;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>CustomDiffMergeToolProvider.LoadCustomDiffMergeTools</c>: under each menu item, the diff (or merge) tools
///  git can start here when there are several, the default (first) bold; for diff tools only while "Show all available
///  difftools" is on, with upstream's "Disable this dropdown", which turns the setting off. The item itself opens the
///  default tool.
/// </summary>
internal static class CustomDiffTools
{
    /// <param name="menus">The items, each with what opens the file in a tool (null for the default one).</param>
    /// <param name="diff">Diff tools, else merge tools.</param>
    public static async Task LoadAsync(IReadOnlyList<(MenuItem Item, Action<string?> Open)> menus, bool diff,
        IAppPreferences preferences)
    {
        Clear(menus);
        if (diff && !preferences.ShowAvailableDiffTools)
        {
            return;
        }

        IReadOnlyList<string> tools = await AppServices.DiffMergeTools.GetAvailableAsync(diff);
        if (tools.Count <= 1)
        {
            return;
        }

        foreach ((MenuItem menu, Action<string?> open) in menus)
        {
            foreach (string tool in tools)
            {
                MenuItem item = new()
                {
                    Header = new TextBlock { Text = tool },
                    Tag = tool,
                    FontWeight = menu.Items.Count == 0 ? FontWeight.Bold : FontWeight.Normal,
                };
                item.Click += (_, _) => open(tool);
                menu.Items.Add(item);
            }

            if (diff)
            {
                MenuItem disable = new() { Header = TranslatedStrings.DisableMenuItem };
                disable.Click += (_, _) =>
                {
                    preferences.ShowAvailableDiffTools = false;
                    preferences.Save();
                    Clear(menus);
                };
                menu.Items.Add(new Separator());
                menu.Items.Add(disable);
            }
        }
    }

    /// <summary>
    ///  Opens the default tool from the item itself; a click on one of its tools also reaches it, hence the source check.
    /// </summary>
    public static void OnClick(MenuItem item, Action<string?> open)
        => item.Click += (_, e) =>
        {
            if (e.Source == item)
            {
                open(null);
            }
        };

    private static void Clear(IReadOnlyList<(MenuItem Item, Action<string?> Open)> menus)
    {
        foreach ((MenuItem item, _) in menus)
        {
            item.Items.Clear();
        }
    }
}
