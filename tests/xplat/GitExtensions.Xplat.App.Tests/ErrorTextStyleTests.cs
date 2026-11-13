using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using AwesomeAssertions;
using GitExtUtils.GitUI.Theming;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class ErrorTextStyleTests
{
    [AvaloniaTest]
    public void ErrorText_class_should_use_the_themed_error_brush_in_light_and_dark_themes()
    {
        ThemeId theme = TestAppBuilder.Preferences.Theme;
        try
        {
            TestAppBuilder.Preferences.Theme = ThemeId.DefaultLight;
            ThemeApplier.Apply(TestAppBuilder.Preferences);
            Color light = ShownErrorColor();

            TestAppBuilder.Preferences.Theme = ThemeId.DefaultDark;
            ThemeApplier.Apply(TestAppBuilder.Preferences);
            Color dark = ShownErrorColor();

            dark.Should().NotBe(light);
        }
        finally
        {
            TestAppBuilder.Preferences.Theme = theme;
            ThemeApplier.Apply(TestAppBuilder.Preferences);
        }
    }

    // The text block and the focused number box both show the brush ThemeApplier set, and the box loses it once valid.
    private static Color ShownErrorColor()
    {
        IBrush expected = (IBrush)Avalonia.Application.Current!.Resources[ThemeApplier.ErrorTextKey]!;
        TextBlock label = new() { Text = "Not valid", Classes = { "errorText" } };
        TextBox box = new() { Text = "abc", Classes = { "errorText" } };
        Window window = new() { Content = new StackPanel { Children = { label, box } } };
        window.Show();
        try
        {
            box.Focus();

            label.Foreground.Should().BeSameAs(expected);
            box.Foreground.Should().BeSameAs(expected);

            box.Classes.Set("errorText", false);
            box.Foreground.Should().NotBeSameAs(expected);
            return ((ISolidColorBrush)expected).Color;
        }
        finally
        {
            window.Close();
        }
    }
}
