using System.Buffers.Binary;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Platform;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class ApplicationMenuTests
{
    // The PNG signature, then the IHDR chunk's length and type, then its width and height.
    private const int PngWidthOffset = 16;
    private const int PngHeaderLength = 24;
    private static readonly byte[] _pngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

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

    // Read at startup on macOS, where a missing picture would stop the app before its window opens. The size is read from
    // the PNG header, as the headless platform decodes no pictures.
    [AvaloniaTest]
    public void The_Dock_icon_should_be_the_512_pixel_logo()
    {
        using Stream logo = AssetLoader.Open(new Uri(App.DockIconAsset));
        byte[] header = new byte[PngHeaderLength];
        logo.ReadExactly(header);

        header[.._pngSignature.Length].Should().Equal(_pngSignature);
        (BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(PngWidthOffset)),
            BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(PngWidthOffset + sizeof(int)))).Should().Be((512, 512));
    }
}
