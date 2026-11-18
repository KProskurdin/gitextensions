using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GitExtensions.Xplat.Core.Presentation;

/// <summary>
///  An item of a menu a view model lists (MVVM.md), for the menus whose items depend on the repository (upstream's
///  drop-downs filled as they open): its text, picture and check, whether it can run, and the command it runs. The view
///  turns the entries into menu items.
/// </summary>
public sealed partial class MenuEntry : ObservableObject
{
    public MenuEntry(string text, ICommand? command = null, object? commandParameter = null)
    {
        Text = text;
        Command = command;
        CommandParameter = commandParameter;
    }

    private MenuEntry()
    {
        Text = "";
        IsSeparator = true;
    }

    public string Text { get; }

    public ICommand? Command { get; }

    public object? CommandParameter { get; }

    /// <summary>
    ///  True when <see cref="Text"/> has an access key (<c>_</c>) to show; otherwise the text is shown as it is.
    /// </summary>
    public bool HasAccessKey { get; init; }

    /// <summary>
    ///  Upstream's picture, by name.
    /// </summary>
    public string? Image { get; init; }

    /// <summary>
    ///  A picture as PNG data, for one that is not upstream's (a plugin's commit template icon); <see cref="Image"/> wins.
    /// </summary>
    public byte[]? ImageData { get; init; }

    public string? Tip { get; init; }

    public bool IsBold { get; init; }

    /// <summary>
    ///  Shown in the theme's gray, as upstream shows what the grid does not show.
    /// </summary>
    public bool IsDimmed { get; init; }

    public bool IsSeparator { get; }

    /// <summary>
    ///  A check item: checked or not; null for a plain item.
    /// </summary>
    public bool? IsChecked { get; init; }

    /// <summary>
    ///  A checked item is one choice of its menu's (a radio item), not a check box.
    /// </summary>
    public bool IsRadio { get; init; }

    /// <summary>
    ///  The hotkey shown on the right, as the view's key gesture.
    /// </summary>
    public object? Gesture { get; init; }

    /// <summary>
    ///  A second text on the right (upstream's shortcut text, such as a repository's branch).
    /// </summary>
    public string? SecondaryText { get; init; }

    /// <summary>
    ///  The text and <see cref="SecondaryText"/> are laid out side by side, also without a second text (upstream's
    ///  repository items, whose branch is their shortcut text).
    /// </summary>
    public bool HasPanelHeader { get; init; }

    /// <summary>
    ///  The text a menu's search box finds the entry by.
    /// </summary>
    public string? SearchText { get; init; }

    /// <summary>
    ///  The item's name, for tests and automation.
    /// </summary>
    public string? Name { get; init; }

    public IReadOnlyList<MenuEntry> Children { get; init; } = [];

    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    public static MenuEntry Separator() => new();
}
