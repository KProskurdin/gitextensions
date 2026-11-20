using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The macOS menu bar's copy of a window's menu (<c>PLAN.md</c> section 9, question 15): the window's <see cref="Menu"/>
///  stays the one definition, which the tests drive. Each submenu is built again from it whenever macOS opens or updates
///  it, so items filled in code, commands' enabled state and check marks are the window menu's; a click runs the window
///  menu's item.
/// </summary>
public static class SystemMenuBar
{
    private static readonly ConditionalWeakTable<NativeMenu, MenuItem> _sources = [];

    /// <summary>
    ///  Shows <paramref name="menu"/> in the system menu bar while <paramref name="window"/> is active, and hides the
    ///  window's own menu once the platform shows it there (macOS; never in the headless tests or on other OSes).
    /// </summary>
    public static void Show(Window window, Menu menu)
    {
        NativeMenu.SetMenu(window, Mirror(menu));
        menu.IsVisible = !NativeMenu.GetIsNativeMenuExported(window);
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == NativeMenu.IsNativeMenuExportedProperty)
            {
                menu.IsVisible = !NativeMenu.GetIsNativeMenuExported(window);
            }
        };
    }

    /// <summary>
    ///  The native menu for <paramref name="menu"/>: one item per top level item, each opening a copy of its submenu.
    /// </summary>
    public static NativeMenu Mirror(Menu menu)
    {
        NativeMenu bar = new();
        foreach (MenuItem item in menu.Items.OfType<MenuItem>())
        {
            NativeMenuItem top = new(Text(item.Header)) { Menu = Submenu(item) };
            Follow(item, top);
            bar.Add(top);
        }

        return bar;
    }

    /// <summary>
    ///  What macOS's opening of a copied submenu does: the window menu's item hears that its submenu opens, and the copy
    ///  is built again from its items. Nothing for a menu that is not a copy.
    /// </summary>
    public static void Open(NativeMenu menu)
    {
        if (_sources.TryGetValue(menu, out MenuItem? source))
        {
            Fill(menu, source, raiseOpened: true);
        }
    }

    /// <summary>
    ///  A menu item's text without its access keys: macOS menus have none, and "__" is a literal underscore.
    /// </summary>
    public static string Text(object? header)
    {
        string text = header?.ToString() ?? "";
        StringBuilder result = new(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == '_' && index + 1 < text.Length)
            {
                index++;
            }

            result.Append(text[index]);
        }

        return result.ToString();
    }

    // The top level items change with the window (the dashboard's menu replaces the repository's, a plugin names the
    // repository hosts menu), so they follow it.
    private static void Follow(MenuItem source, NativeMenuItem target)
    {
        target.IsVisible = source.IsVisible;
        target.IsEnabled = source.IsEnabled;
        source.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.IsVisibleProperty)
            {
                target.IsVisible = source.IsVisible;
            }
            else if (e.Property == MenuItem.IsEnabledProperty)
            {
                target.IsEnabled = source.IsEnabled;
            }
            else if (e.Property == MenuItem.HeaderProperty)
            {
                target.Header = Text(source.Header);
            }
        };
    }

    private static NativeMenu Submenu(MenuItem source)
    {
        NativeMenu menu = new();
        _sources.Add(menu, source);

        // Opening when the user opens it; NeedsUpdate also before macOS looks for a key's item.
        menu.Opening += (_, _) => Open(menu);
        menu.NeedsUpdate += (_, _) => Open(menu);
        Fill(menu, source, raiseOpened: false);
        return menu;
    }

    // The window menu's own opening event comes first, as some submenus refresh their items on it (the View menu).
    private static void Fill(NativeMenu menu, MenuItem source, bool raiseOpened)
    {
        if (raiseOpened)
        {
            source.RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent, source));
        }

        menu.Items.Clear();
        foreach (object? child in source.Items)
        {
            switch (child)
            {
                case Separator:
                    menu.Add(new NativeMenuItemSeparator());
                    break;
                case MenuItem item:
                    menu.Add(Item(item));
                    break;
            }
        }
    }

    // Without the window item's key: the window handles its keys itself, and a key macOS also gave the menu item might run
    // twice, which turns a toggle back silently. To try at the screen (MACOS.md section 10) before the menu shows keys.
    private static NativeMenuItem Item(MenuItem source)
    {
        NativeMenuItem item = new(Text(source.Header))
        {
            IsVisible = source.IsVisible,
            IsEnabled = source.IsEnabled
                        && (source.Command is not { } command || command.CanExecute(source.CommandParameter)),
            ToggleType = source.ToggleType,
            IsChecked = source.IsChecked,
            ToolTip = ToolTip.GetTip(source) as string,
        };
        if (source.ItemCount > 0)
        {
            item.Menu = Submenu(source);
        }
        else
        {
            item.Click += (_, _) => Click(source);
        }

        return item;
    }

    // As a click on the window menu's item: its click handlers, and its command when its own click handling did not run
    // it (an item of a submenu that never opened is not in a visual tree).
    private static void Click(MenuItem source)
    {
        RoutedEventArgs args = new(MenuItem.ClickEvent, source);
        source.RaiseEvent(args);
        if (!args.Handled && source.Command is ICommand command && command.CanExecute(source.CommandParameter))
        {
            command.Execute(source.CommandParameter);
        }
    }
}
