using System.Drawing;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Settings;
using GitExtUtils.GitUI.Theming;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class ImageLightnessTests
{
    private static readonly ImageLightness _dark = new(Color.FromArgb(230, 230, 230), Color.FromArgb(30, 30, 30));

    [Test]
    public void Transform_should_map_a_white_page_to_the_background_and_black_lines_to_the_text()
    {
        Lightness(_dark.Transform(Color.White)).Should().BeApproximately(Lightness(Color.FromArgb(30, 30, 30)), 0.02);
        Lightness(_dark.Transform(Color.Black)).Should().BeApproximately(Lightness(Color.FromArgb(230, 230, 230)), 0.02);
    }

    [Test]
    public void Transform_should_keep_a_colors_hue()
    {
        Color red = _dark.Transform(Color.FromArgb(200, 30, 30));

        red.R.Should().BeGreaterThan(red.G);
        red.R.Should().BeGreaterThan(red.B);
    }

    [Test]
    public void Transform_should_leave_the_default_themes_colors_as_they_are()
    {
        ImageLightness light = new(Color.Black, Color.White);

        foreach (Color color in new[] { Color.White, Color.Black, Color.FromArgb(40, 120, 200) })
        {
            Color same = light.Transform(color);
            Math.Abs(same.R - color.R).Should().BeLessThan(3);
            Math.Abs(same.G - color.G).Should().BeLessThan(3);
            Math.Abs(same.B - color.B).Should().BeLessThan(3);
        }
    }

    [TestCase(false, 2)]
    [TestCase(true, 0)]
    public void TransformRow_should_map_each_pixel_in_either_byte_order(bool redFirst, int red)
    {
        byte[] row =
        [
            255, 255, 255, 255,
            0, 0, 0, 255,
        ];

        _dark.TransformRow(row, redFirst, premultiplied: false);

        row[red].Should().BeInRange(25, 40, "white becomes the background");
        row[3].Should().Be(255);
        row[4 + red].Should().BeInRange(220, 240, "black becomes the text");
        row[7].Should().Be(255);
    }

    [Test]
    public void TransformRow_should_keep_the_opacity_and_scale_a_premultiplied_color_by_it()
    {
        byte[] row =
        [
            0, 0, 0, 128,
            0, 0, 0, 0,
        ];

        _dark.TransformRow(row, redFirst: false, premultiplied: true);

        row[2].Should().BeInRange(105, 125, "black at half opacity becomes the text at half opacity");
        row[3].Should().Be(128);
        row[4..].Should().Equal(0, 0, 0, 0);
    }

    [Test]
    public void TransformRow_should_read_a_premultiplied_color_as_the_color_it_stands_for()
    {
        // White at half opacity: premultiplied it reads 128, which is not grey.
        byte[] row = [128, 128, 128, 128];

        _dark.TransformRow(row, redFirst: false, premultiplied: true);

        row[2].Should().BeInRange(10, 22, "white becomes the background, at half opacity");
    }

    private static double Lightness(Color color) => new HslColor(color).L;
}
