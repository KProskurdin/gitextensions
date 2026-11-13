using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Clicks as a user does, for controls bound to a view model's commands (MVVM.md). Raising <see cref="Button.ClickEvent"/>
///  alone runs no command, and Avalonia 12 made the controls' own click methods internal, so a button bound to a command
///  gets a mouse click on its middle, and a menu item bound to one runs it after its click event, as its menu does. Every
///  other control and event is raised as the tests raised it before the windows moved to view models.
/// </summary>
internal static class UiClicks
{
    public static void Click(Control control)
        => Raise(control, control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent);

    public static void Raise(Control control, RoutedEvent routedEvent)
    {
        if (routedEvent == Button.ClickEvent && control is Button { Command: not null } button)
        {
            MouseClick(button);
            return;
        }

        control.RaiseEvent(new RoutedEventArgs(routedEvent));
        if (routedEvent == MenuItem.ClickEvent && control is MenuItem { Command: ICommand command } item
                                               && command.CanExecute(item.CommandParameter))
        {
            command.Execute(item.CommandParameter);
        }
    }

    private static void MouseClick(Button button)
    {
        TopLevel window = TopLevel.GetTopLevel(button) ??
                          throw new InvalidOperationException($"{button.Name} is not shown");

        // A window shown a moment ago may not be laid out yet, and the click goes to the button's place.
        window.UpdateLayout();
        Point middle = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)
                       ?? throw new InvalidOperationException($"{button.Name} is not in its window");
        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
    }
}
