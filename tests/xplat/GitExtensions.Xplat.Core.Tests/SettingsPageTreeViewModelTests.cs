using AwesomeAssertions;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.Plugins;
using GitExtensions.Xplat.Core.Settings;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class SettingsPageTreeViewModelTests
{
    private static readonly string[] _pages =
    [
        "ChecklistSettingsPage", "GeneralSettingsPage", "AppearanceSettingsPage", "ColorsSettingsPage",
        "AppearanceFontsSettingsPage", "GitSettingsPage", "GitConfigSettingsPage", "PluginRootIntroductionPage",
    ];

    [Test]
    public void The_tree_should_hold_the_windows_pages_under_upstreams_groups()
    {
        SettingsPageTreeViewModel tree = New();

        tree.Roots.Select(node => node.Title).Should().Equal("Git Extensions", "Git", "Plugins");
        tree.Roots[0].Page.Should().Be(SettingsPageTreeViewModel.ChecklistPage, "the Checklist is the group's own page");
        tree.Roots[0].Children.Select(node => node.Title).Should().Equal("General", "Appearance");
        tree.Roots[0].Children[1].Children.Select(node => node.Title).Should().Equal("Colors", "Fonts");
        tree.Roots[1].Page.Should().BeNull("the Git group's introduction is not one of the window's pages");
        tree.SearchPlaceholder.Should().Be(SettingsPageTree.TypeToFind);
    }

    [Test]
    public void A_group_without_a_page_should_show_its_first_page_and_the_title_should_name_it()
    {
        SettingsPageTreeViewModel tree = New();

        tree.GoToPage(tree.Roots[1]);

        tree.ShownNode!.Page.Should().Be("GitSettingsPage");
        tree.SelectedNode.Should().BeSameAs(tree.ShownNode);
        tree.Title.Should().Be("Settings - Paths");
        tree.IsInstantSavePage.Should().BeFalse();
        tree.Roots[1].IsExpanded.Should().BeTrue();

        tree.SelectedNode = tree.Roots[0];

        tree.ShownNode.Page.Should().Be(SettingsPageTreeViewModel.ChecklistPage);
        tree.Title.Should().Be("Settings - Checklist");
        tree.IsInstantSavePage.Should().BeTrue("the Checklist saves at once");
    }

    [Test]
    public void The_search_should_mark_the_pages_it_finds_and_find_next_should_show_them_in_turn()
    {
        SettingsPageTreeViewModel tree = New(node => node.Page == "AppearanceFontsSettingsPage" ? ["Code font"] : []);
        tree.GoToPage("GeneralSettingsPage");

        tree.SearchText = "font";

        All(tree).Where(node => node.IsFound).Select(node => node.Title).Should().Equal("Fonts");
        tree.Roots[0].Children[1].IsExpanded.Should().BeTrue();

        tree.SearchText = "co";
        All(tree).Where(node => node.IsFound).Select(node => node.Title).Should().Equal("Colors", "Fonts", "Config");
        tree.FindNextCommand.Execute(null);
        tree.ShownNode!.Title.Should().Be("Colors");
        tree.FindNextCommand.Execute(null);
        tree.ShownNode.Title.Should().Be("Fonts");
        tree.FindNextCommand.Execute(null);
        tree.FindNextCommand.Execute(null);
        tree.ShownNode.Title.Should().Be("Colors", "the search starts again after the last page");

        tree.SearchText = "";
        All(tree).Should().NotContain(node => node.IsFound);
    }

    [Test]
    public void A_plugins_page_should_be_shown_first_when_asked_for_and_named_in_the_title()
    {
        IGitPlugin plugin = Substitute.For<IGitPlugin>();
        plugin.Name.Returns("Gource");
        SettingsPageTreeViewModel tree = New(pluginPages: [new PluginOption(Substitute.For<IGitPlugin>()), new PluginOption(plugin)]);

        tree.GoToInitialPage(pluginsPage: true, plugin);

        tree.ShownNode!.Plugin.Should().Be(new PluginOption(plugin));
        tree.ShownNode.Page.Should().Be(SettingsPageTreeViewModel.PluginPage);
        tree.Title.Should().Be("Settings - Plugin: Gource");
    }

    private static SettingsPageTreeViewModel New(Func<SettingsPageNode, IEnumerable<string>>? pageTexts = null,
        IEnumerable<object>? pluginPages = null)
        => new(new SettingsPageTreeServices { Pages = _pages.ToHashSet(), PageTexts = pageTexts ?? (_ => []) },
            pluginPages ?? []);

    private static IEnumerable<SettingsPageNode> All(SettingsPageTreeViewModel tree)
    {
        return Descendants(tree.Roots);

        static IEnumerable<SettingsPageNode> Descendants(IEnumerable<SettingsPageNode> nodes)
            => nodes.SelectMany(node => (IEnumerable<SettingsPageNode>)[node, .. Descendants(node.Children)]);
    }
}
