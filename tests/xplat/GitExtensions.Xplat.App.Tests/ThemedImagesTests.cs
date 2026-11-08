using Avalonia.Headless.NUnit;
using Avalonia.Media.Imaging;
using AwesomeAssertions;
using GitExtUtils.GitUI.Theming;
using NUnit.Framework;
using DrawingColor = System.Drawing.Color;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  The headless platform's bitmaps keep no pixels, so what happens to the pixels is tested in <c>ImageLightnessTests</c>.
/// </summary>
internal sealed class ThemedImagesTests
{
    private static readonly DrawingColor _text = DrawingColor.FromArgb(230, 230, 230);
    private static readonly DrawingColor _background = DrawingColor.FromArgb(30, 30, 30);

    [AvaloniaTest]
    public void Adapted_should_keep_the_size_and_give_the_same_picture_for_the_same_colors_only()
    {
        Bitmap picture = UpstreamImages.Named("HelpCommandMerge")!;

        Bitmap adapted = ThemedImages.Adapted(picture, _text, _background);

        adapted.PixelSize.Should().Be(picture.PixelSize);
        ThemedImages.Adapted(picture, _text, _background).Should().BeSameAs(adapted);
        ThemedImages.Adapted(picture, DrawingColor.Black, DrawingColor.White).Should().NotBeSameAs(adapted);
    }

    [AvaloniaTest]
    public void AdaptLightness_should_leave_a_picture_alone_in_the_default_theme_and_adapt_it_in_a_dark_one()
    {
        Bitmap picture = UpstreamImages.Named("HelpCommandMerge")!;
        ThemeId theme = TestAppBuilder.Preferences.Theme;
        try
        {
            TestAppBuilder.Preferences.Theme = ThemeId.DefaultLight;
            ThemeApplier.Apply(TestAppBuilder.Preferences);
            ThemedImages.AdaptLightness(picture).Should().BeSameAs(picture);
            ThemedImages.AdaptLightness(null).Should().BeNull();

            TestAppBuilder.Preferences.Theme = ThemeId.DefaultDark;
            ThemeApplier.Apply(TestAppBuilder.Preferences);
            ThemedImages.AdaptLightness(picture).Should().NotBeSameAs(picture);
        }
        finally
        {
            TestAppBuilder.Preferences.Theme = theme;
            ThemeApplier.Apply(TestAppBuilder.Preferences);
        }
    }
}
