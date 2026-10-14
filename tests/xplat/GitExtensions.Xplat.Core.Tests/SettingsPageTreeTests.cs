using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class SettingsPageTreeTests
{
    private static readonly string _settingsDialog =
        Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "CommandsDialogs", "SettingsDialog");

    [Test]
    public void The_pages_parents_and_images_are_upstreams_in_upstreams_order()
    {
        string source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "CommandsDialogs",
            "FormSettings.cs"));
        Dictionary<string, string> types = Regex.Matches(source,
                @"(?<type>\w+) (?<var>\w+) = SettingsPageBase\.Create<\k<type>>")
            .ToDictionary(match => match.Groups["var"].Value, match => match.Groups["type"].Value);
        foreach (Match match in Regex.Matches(source,
                     @"SettingsPageReference (?<var>\w+) = (?<type>\w+)\.GetPageReference\(\);"))
        {
            types[match.Groups["var"].Value] = match.Groups["type"].Value;
        }

        List<SettingsPageEntry> upstream = [];
        foreach (Match match in Regex.Matches(source,
                     @"settingsTreeView\.AddSettingsPage\((?:SettingsPageBase\.Create<(?<type>\w+)>\(this, serviceProvider\)" +
                     @"|new (?<type>\w+)\(\)|(?<var>\w+)), (?<parent>\w+), (?:icon: null|Images\.(?<image>\w+)" +
                     @"(?<adapt>\.AdaptLightness\(\))?)(?<root>, asRoot: true)?\);"))
        {
            string page = match.Groups["type"].Success ? match.Groups["type"].Value : types[match.Groups["var"].Value];
            string parent = match.Groups["parent"].Value;
            upstream.Add(new SettingsPageEntry(page, Title(page), parent == "null" ? null : types[parent],
                match.Groups["image"].Success ? match.Groups["image"].Value : null, match.Groups["adapt"].Success,
                match.Groups["root"].Success));
        }

        upstream.Should().HaveCount(28);
        SettingsPageTree.Entries.Should().Equal(upstream);
    }

    [Test]
    public void The_search_box_placeholder_is_upstreams()
        => File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "TranslatedStrings.cs"))
            .Should().Contain($"new(\"{SettingsPageTree.TypeToFind}\")");

    [TestCase("fonts", true)]
    [TestCase("FON", true)]
    [TestCase("code font", true)]
    [TestCase("font code", true)]
    [TestCase("font diff", false)]
    [TestCase("size", true)]
    [TestCase("", false)]
    [TestCase("   ", false)]
    public void Matches_should_find_a_page_by_its_title_or_by_every_word_in_its_texts(string search, bool found)
        => SettingsPageTree.Matches(search, "Fonts", ["Application font", "Code font", "Sizes are in points."]).Should()
            .Be(found);

    [Test]
    public void Matches_should_find_by_the_whole_text_in_the_title_only()
    {
        SettingsPageTree.Matches("diff viewer", "Diff viewer", []).Should().BeTrue();
        SettingsPageTree.Matches("viewer diff", "Diff viewer", []).Should().BeFalse();
    }

    // The page's designer title, or the group's name.
    private static string Title(string page)
    {
        string groups = File.ReadAllText(Path.Combine(_settingsDialog, "SettingsGroupPages.cs"));
        Match group = Regex.Match(groups, $@"public {page}\(\)\s*: base\((?<title>[^)]+)\)");
        if (group.Success)
        {
            string title = group.Groups["title"].Value;
            return title == "AppSettings.ApplicationName" ? "Git Extensions" : title.Trim('"');
        }

        string designer = File.ReadAllText(Directory.GetFiles(_settingsDialog, $"{page}.Designer.cs",
            SearchOption.AllDirectories).Single());
        return Regex.Matches(designer, @"^\s*Text = ""(?<title>[^""]+)"";", RegexOptions.Multiline)[^1]
            .Groups["title"].Value;
    }

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
