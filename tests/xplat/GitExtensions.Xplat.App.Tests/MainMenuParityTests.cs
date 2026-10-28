using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Reads upstream's menu designers (UI-PARITY principle 1): the browse window's Start, Dashboard, Repository, Commands,
///  Tools and Help menus have upstream's texts and access keys, and their items come in upstream's order.
/// </summary>
internal sealed partial class MainMenuParityTests
{
    private const string FormBrowse = "src/app/GitUI/CommandsDialogs/FormBrowse.Designer.cs";
    private const string Menus = "src/app/GitUI/CommandsDialogs/Menus/";

    // The new shell's own items, which upstream does not have.
    private static readonly string[] _newShellItems = ["Browse _files..."];

    [AvaloniaTest]
    [TestCase("_Start", Menus + "StartToolStripMenuItem.Designer.cs", null)]
    [TestCase("_Dashboard", FormBrowse, "dashboardToolStripMenuItem")]
    [TestCase("_Repository", FormBrowse, "repositoryToolStripMenuItem")]
    [TestCase("_Commands", FormBrowse, "commandsToolStripMenuItem")]
    [TestCase("_Tools", Menus + "ToolsToolStripMenuItem.Designer.cs", null)]
    [TestCase("_Help", Menus + "HelpToolStripMenuItem.Designer.cs", null)]
    public void A_menu_has_upstreams_items_in_upstreams_order(string header, string designer, string? menu)
    {
        (string text, IReadOnlyList<string> upstream) = UpstreamMenu(UpstreamSource(designer), menu);
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));

        MenuItem ours = window.FindControl<Menu>("MainMenu")!.Items.OfType<MenuItem>()
            .Single(item => (string?)item.Header == header);
        List<string> items =
        [
            .. ours.Items.OfType<MenuItem>().Select(item => item.Header as string ?? "")
                .Where(item => !_newShellItems.Contains(item))
        ];

        text.Should().Be(header);
        items.Should().Equal(upstream.Where(items.Contains), "the items come in upstream's order");
        items.Except(upstream).Should().BeEmpty("every item has upstream's text and access key");
    }

    // A ToolStripMenuItem's Text and its drop-down items' texts, with Avalonia's "_" for upstream's "&". A null menu is the
    // designer's own item (the Menus folder's classes).
    private static (string Text, IReadOnlyList<string> Items) UpstreamMenu(string source, string? menu)
    {
        Dictionary<string, string> texts = [];
        foreach (Match match in TextAssignment().Matches(source))
        {
            texts.TryAdd(match.Groups["item"].Success ? match.Groups["item"].Value : "", match.Groups["text"].Value);
        }

        Match items = DropDownItems().Matches(source).First(match => match.Groups["menu"].Value == (menu ?? ""));
        return (Avalonia(texts[menu ?? ""]),
        [
            .. items.Groups["items"].Value.Split(',').Select(item => item.Trim())
                .Where(item => item.Length > 0 && texts.ContainsKey(item)).Select(item => Avalonia(texts[item]))
        ]);

        static string Avalonia(string text) => text.Replace("&", "_", StringComparison.Ordinal);
    }

    private static string UpstreamSource(string relativePath, [System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", relativePath)));

    [GeneratedRegex(@"^\s*(?:(?<item>\w+)\.)?Text = ""(?<text>[^""]*)"";", RegexOptions.Multiline)]
    private static partial Regex TextAssignment();

    [GeneratedRegex(@"(?:(?<menu>\w+)\.)?DropDownItems\.AddRange\(new ToolStripItem\[\]\s*\{(?<items>[^}]*)\}")]
    private static partial Regex DropDownItems();
}
