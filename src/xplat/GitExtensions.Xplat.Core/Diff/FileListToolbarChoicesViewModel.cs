using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  An item of a file list's toolbar as upstream's <c>tsmiToolbar</c> lists it: upstream's control name, which its setting
///  is named after, its menu text and picture, and whether it is the settings button, which cannot be hidden.
/// </summary>
public sealed record FileListToolbarChoice(string UpstreamName, string Text, string? Image, bool IsSettingsButton);

/// <summary>
///  Upstream's <c>tsmiToolbar</c> (<c>FileStatusList.UpdateToolbar</c>): which items a file list's toolbar shows, under
///  upstream's settings, so one choice holds for every file list and in both apps. The view hides an item on
///  <see cref="ItemShown"/>.
/// </summary>
public sealed partial class FileListToolbarChoicesViewModel(IAppPreferences preferences,
    IReadOnlyList<FileListToolbarChoice> items)
{
    public const string ToolbarMenuText = "_Toolbar";

    /// <summary>
    ///  Raised when an item was shown or hidden: its upstream name and whether it shows now.
    /// </summary>
    public event EventHandler<(string UpstreamName, bool Visible)>? ItemShown;

    public IReadOnlyList<FileListToolbarChoice> Items => items;

    public bool IsVisible(string upstreamName) => preferences.IsFileListToolbarItemVisible(upstreamName);

    /// <summary>
    ///  The "Toolbar" submenu of the list's settings menu: a check item per toolbar item.
    /// </summary>
    public MenuEntry MenuEntry(string text) => new(text)
    {
        HasAccessKey = true,
        Children =
        [
            .. items.Select(item => new MenuEntry(item.Text, ToggleItemCommand, item.UpstreamName)
            {
                // The texts are upstream's tooltips: no access keys, but shown as text, as upstream's menu does.
                HasAccessKey = true,
                IsChecked = IsVisible(item.UpstreamName),
                Image = item.Image,
                IsEnabled = !item.IsSettingsButton,
            })
        ],
    };

    [RelayCommand]
    private void ToggleItem(string upstreamName)
    {
        bool visible = !IsVisible(upstreamName);
        preferences.SetFileListToolbarItemVisible(upstreamName, visible);
        preferences.Save();
        ItemShown?.Invoke(this, (upstreamName, visible));
    }
}
