using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Shows a view model's <see cref="MenuEntry"/> list as menu items (MVVM.md): the view's part of the menus filled as
///  they open. An item's enabled state and visibility follow its entry; a click runs the entry's command.
/// </summary>
internal static class MenuEntries
{
    /// <param name="decorate">The view's own handling of an item, such as the keys held when it is clicked.</param>
    public static void Fill(ItemCollection items, IEnumerable<MenuEntry> entries, IResourceHost resources,
        Action<MenuItem, MenuEntry>? decorate = null)
    {
        items.Clear();
        foreach (MenuEntry entry in entries)
        {
            items.Add(Item(entry, resources, decorate));
        }
    }

    public static Control Item(MenuEntry entry, IResourceHost resources, Action<MenuItem, MenuEntry>? decorate = null)
    {
        if (entry.IsSeparator)
        {
            return new Separator();
        }

        MenuItem item = new()
        {
            Header = Header(entry, resources),
            Icon = entry.Image is { } image && UpstreamImages.Named(image) is { } picture ? ToolStripContent.Icon(picture) : null,
            Command = entry.Command,
            CommandParameter = entry.CommandParameter,
            Tag = entry.SearchText,
            InputGesture = entry.Gesture as KeyGesture,
            DataContext = entry,
        };
        if (entry.IsBold)
        {
            item.FontWeight = FontWeight.Bold;
        }

        if (entry.Name is { } name)
        {
            item.Name = name;
        }

        if (entry.IsChecked is { } isChecked)
        {
            item.ToggleType = entry.IsRadio ? MenuItemToggleType.Radio : MenuItemToggleType.CheckBox;
            item.IsChecked = isChecked;
        }

        if (entry.Tip is { } tip)
        {
            ToolTip.SetTip(item, tip);
        }

        item.Bind(InputElement.IsEnabledProperty, new Binding(nameof(MenuEntry.IsEnabled)) { Mode = BindingMode.OneWay });
        item.Bind(Visual.IsVisibleProperty, new Binding(nameof(MenuEntry.IsVisible)) { Mode = BindingMode.OneWay });
        if (entry.Children.Count > 0)
        {
            item.ItemsSource = entry.Children.Select(child => Item(child, resources, decorate)).ToList();
        }

        decorate?.Invoke(item, entry);
        return item;
    }

    // An access key's text is the header itself, as upstream's menus show it; other texts show as they are.
    private static object Header(MenuEntry entry, IResourceHost resources)
    {
        if (entry.HasAccessKey && !entry.HasPanelHeader && entry.SecondaryText is null)
        {
            return entry.Text;
        }

        TextBlock text = entry.HasAccessKey
            ? new AccessText { Text = entry.Text, VerticalAlignment = VerticalAlignment.Center }
            : new TextBlock { Text = entry.Text, FontWeight = entry.IsBold ? FontWeight.Bold : FontWeight.Normal };
        if (entry.IsDimmed)
        {
            text.Bind(TextBlock.ForegroundProperty, resources.GetResourceObservable(ThemeApplier.DisabledTextKey));
        }

        if (entry.SecondaryText is null && !entry.HasPanelHeader)
        {
            return text;
        }

        DockPanel header = new() { LastChildFill = true };
        if (entry.SecondaryText is { } secondary)
        {
            TextBlock right = new() { Text = secondary, Margin = new Thickness(24, 0, 0, 0), Opacity = 0.7 };
            DockPanel.SetDock(right, Dock.Right);
            header.Children.Add(right);
        }

        header.Children.Add(text);
        return header;
    }
}
