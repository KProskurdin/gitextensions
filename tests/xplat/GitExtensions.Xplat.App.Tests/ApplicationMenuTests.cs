using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class ApplicationMenuTests
{
    [AvaloniaTest]
    public void The_menu_offers_About_and_Settings_with_the_settings_hotkey()
    {
        int about = 0;
        int settings = 0;
        KeyGesture gesture = new(Key.OemComma, Hotkeys.CommandModifier);

        NativeMenu menu = App.ApplicationMenu(gesture, () => about++, () => settings++);

        List<NativeMenuItem> items = [.. menu.Items.OfType<NativeMenuItem>().Where(item => item is not NativeMenuItemSeparator)];
        items.Select(item => item.Header).Should().Equal("About Git Extensions", "Settings...");
        items[1].Gesture.Should().Be(gesture);
        ((INativeMenuItemExporterEventsImplBridge)items[0]).RaiseClicked();
        ((INativeMenuItemExporterEventsImplBridge)items[1]).RaiseClicked();
        (about, settings).Should().Be((1, 1));
    }
}
