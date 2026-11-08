using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class WindowIconTests
{
    [AvaloniaTest]
    public void A_window_of_the_app_has_the_app_icon_for_the_taskbar()
    {
        AboutWindow window = new();
        window.Show();

        window.Icon.Should().NotBeNull();

        window.Close();
    }

    [AvaloniaTest]
    public void A_window_that_knows_nothing_of_the_app_has_it_too()
    {
        // As a plugin's window, which no code of the app sets anything on.
        Window window = new();
        window.Show();

        window.Icon.Should().NotBeNull();

        window.Close();
    }
}
