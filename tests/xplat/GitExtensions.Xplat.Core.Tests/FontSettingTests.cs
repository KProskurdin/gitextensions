using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class FontSettingTests
{
    [TestCase("Consolas;10", "Consolas", 10f, false, false)]
    [TestCase("Segoe UI;9.75;_IC_", "Segoe UI", 9.75f, false, false)]
    [TestCase("Segoe UI;9,75", "Segoe UI", 9.75f, false, false)]
    [TestCase("Consolas;10;_IC_;1;0", "Consolas", 10f, true, false)]
    [TestCase("DejaVu Sans Mono;11.5;_IC_;0;1", "DejaVu Sans Mono", 11.5f, false, true)]
    public void Parse_should_read_what_upstreams_FontParser_reads(string value, string family, float size, bool bold,
        bool italic)
    {
        FontSetting.Parse(value).Should().Be(new FontSetting(family, size, bold, italic));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Consolas")]
    [TestCase("Consolas;big")]
    [TestCase("Consolas;0")]
    [TestCase(";10")]
    public void Parse_should_give_null_where_upstream_falls_back_to_its_default(string? value)
    {
        FontSetting.Parse(value).Should().BeNull();
    }

    [Test]
    public void ToSettingString_should_write_upstreams_format_and_read_back()
    {
        FontSetting font = new("Segoe UI", 9.75f, Bold: true);

        font.ToSettingString().Should().Be("Segoe UI;9.75;_IC_;1;0");
        FontSetting.Parse(font.ToSettingString()).Should().Be(font);
        new FontSetting("Consolas", 10).ToSettingString().Should().Be("Consolas;10;_IC_;0;0");
    }

    [Test]
    public void Sizes_should_be_shown_rounded_and_converted_to_pixels()
    {
        new FontSetting("Consolas", 9.5f).ToString().Should().Be("Consolas, 10");
        new FontSetting("Segoe UI", 9).SizeInPixels.Should().Be(12);
    }

    [Test]
    public void The_keys_and_format_are_upstreams()
    {
        string settings = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitCommands", "Settings",
            "AppSettings.cs"));
        string parser = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitExtensions.Extensibility",
            "FontParser.cs"));
        string page = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "CommandsDialogs",
            "SettingsDialog", "Pages", "AppearanceFontsSettingsPage.Designer.cs"));

        foreach (AppFont font in Enum.GetValues<AppFont>())
        {
            settings.Should().Contain($"GetFont(\"{FontSetting.KeyOf(font)}\"");
        }

        parser.Should().Contain("\"{0};{1};{2};{3};{4}\"").And.Contain("\"_IC_\"");
        foreach (string label in (string[])["Application font", "Commit font", "Code font", "Monospace font"])
        {
            page.Should().Contain($".Text = \"{label}\";");
        }
    }

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
