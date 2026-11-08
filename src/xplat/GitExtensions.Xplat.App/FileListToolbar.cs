using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>tsmiToolbar</c> (<c>FileStatusList.UpdateToolbar</c>): which items a file list's toolbar shows, under
///  upstream's settings, so one choice holds for every file list and in both apps.
/// </summary>
/// <remarks>
///  The toolbars' own rules set <c>IsVisible</c> as the list changes, for instance the status filter while the list has a BASE
///  group. A hidden item therefore takes no room through a style instead, which works whatever those rules set.
/// </remarks>
public static class FileListToolbar
{
    /// <summary>
    ///  Gets the style class of an item the user has hidden (App.axaml).
    /// </summary>
    public const string HiddenClass = "toolHiddenByUser";

    private const string ToolbarMenuText = "_Toolbar";
    private const string SeparatorText = "Separator '{0}'";

    // Upstream's name of each item, by the end of the fork's control name, and of the separator before it.
    private static readonly (string Suffix, string Item, string? SeparatorBefore)[] _items =
    [
        ("CollapseGroupsButton", "btnCollapseGroups", null),
        ("RefreshButton", "btnRefresh", "sepRefresh"),
        ("AsTreeButton", "btnAsTree", "sepAsTree"),
        ("ByPathButton", "btnByPath", "sepGroupBy"),
        ("ByExtensionButton", "btnByExtension", null),
        ("ByStatusButton", "btnByStatus", null),
        ("UnequalChangeButton", "btnUnequalChange", "sepFilter"),
        ("OnlyBButton", "btnOnlyB", null),
        ("OnlyAButton", "btnOnlyA", null),
        ("SameChangeButton", "btnSameChange", null),
        ("GitGrepButton", "btnFindInFilesGitGrep", "sepOptions"),
        ("SettingsButton", "btnSettings", "sepSettings"),
    ];

    /// <summary>
    ///  Applies the stored choice to the toolbar that holds <paramref name="anyItem"/>, and with a settings menu adds upstream's
    ///  submenu to it. Upstream's settings button itself cannot be hidden.
    /// </summary>
    public static IReadOnlyList<FileListToolbarItem> Attach(Control anyItem, IAppPreferences preferences, MenuFlyout? settingsMenu = null)
    {
        IReadOnlyList<FileListToolbarItem> items = ItemsOf(anyItem);
        foreach (FileListToolbarItem item in items)
        {
            Show(item, preferences.IsFileListToolbarItemVisible(item.UpstreamName));
        }

        if (settingsMenu is not null)
        {
            settingsMenu.Items.Add(new Separator());
            settingsMenu.Items.Add(NewMenu(items, preferences));
        }

        return items;
    }

    private static MenuItem NewMenu(IReadOnlyList<FileListToolbarItem> items, IAppPreferences preferences)
    {
        MenuItem menu = new() { Header = UpstreamTranslation.ByText(ToolbarMenuText) };
        foreach (FileListToolbarItem item in items)
        {
            MenuItem entry = new()
            {
                Header = item.Text,
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = preferences.IsFileListToolbarItemVisible(item.UpstreamName),
                IsEnabled = !item.IsSettingsButton,
                Icon = item.Image is { } image && UpstreamImages.Named(image) is { } picture ? ToolStripContent.Icon(picture) : null,
            };
            entry.Click += (_, _) =>
            {
                preferences.SetFileListToolbarItemVisible(item.UpstreamName, entry.IsChecked);
                preferences.Save();
                Show(item, entry.IsChecked);
            };
            menu.Items.Add(entry);
        }

        return menu;
    }

    private static void Show(FileListToolbarItem item, bool visible) => item.Control.Classes.Set(HiddenClass, !visible && !item.IsSettingsButton);

    private static IReadOnlyList<FileListToolbarItem> ItemsOf(Control anyItem)
    {
        if (anyItem.Parent is not Panel strip)
        {
            return [];
        }

        List<FileListToolbarItem> items = [];
        Control[] children = [.. strip.Children];
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] is Rectangle separator)
            {
                if (i + 1 < children.Length && Known(children[i + 1]) is { SeparatorBefore: { } name })
                {
                    items.Add(new FileListToolbarItem(name, separator, string.Format(UpstreamTranslation.ByText(SeparatorText), TipOf(children[i + 1])), null, false));
                }
            }
            else if (Known(children[i]) is { } known)
            {
                items.Add(new FileListToolbarItem(known.Item, children[i], TipOf(children[i]), ImageOf(children[i]), known.Item == _items[^1].Item));
            }
        }

        return items;
    }

    private static (string Suffix, string Item, string? SeparatorBefore)? Known(Control control)
    {
        foreach ((string Suffix, string Item, string? SeparatorBefore) item in _items)
        {
            if (control.Name?.EndsWith(item.Suffix, StringComparison.Ordinal) == true)
            {
                return item;
            }
        }

        return null;
    }

    private static string TipOf(Control control) => ToolTip.GetTip(control) as string ?? "";

    private static string? ImageOf(Control control) => control switch
    {
        ToolButton button => button.Image,
        ToolToggleButton button => button.Image,
        ToolDropDownButton button => button.Image,
        ToolSplitButton button => button.Image,
        _ => null,
    };
}

/// <summary>
///  An item of a file list's toolbar: upstream's control name, which its setting is named after, and upstream's menu text.
/// </summary>
public sealed record FileListToolbarItem(string UpstreamName, Control Control, string Text, string? Image, bool IsSettingsButton);
