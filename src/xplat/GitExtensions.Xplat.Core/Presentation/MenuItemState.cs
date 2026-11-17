using CommunityToolkit.Mvvm.ComponentModel;

namespace GitExtensions.Xplat.Core.Presentation;

/// <summary>
///  The state of one menu item that a view model computes as its menu opens (upstream's <c>SetEnabled</c> and
///  <c>Visible</c> calls): whether it shows, whether it can run, and, for an item whose text depends on what the menu opens
///  on, its text and tooltip. The view binds the item to it.
/// </summary>
public sealed partial class MenuItemState : ObservableObject
{
    public MenuItemState(string? text = null, string? tip = null)
    {
        Text = text;
        Tip = tip;
    }

    [ObservableProperty]
    public partial bool IsVisible { get; private set; }

    [ObservableProperty]
    public partial bool IsEnabled { get; private set; }

    [ObservableProperty]
    public partial string? Text { get; set; }

    [ObservableProperty]
    public partial string? Tip { get; set; }

    /// <summary>
    ///  True when the item shows and can run.
    /// </summary>
    public bool IsOffered => IsVisible && IsEnabled;

    /// <summary>
    ///  Shows the item, enabled when it can run now, or hides it.
    /// </summary>
    public void Show(bool visible, bool enabled = true)
    {
        IsVisible = visible;
        IsEnabled = visible && enabled;
    }
}
