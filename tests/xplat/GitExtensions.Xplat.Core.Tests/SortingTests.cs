using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class SortingTests
{
    [Test]
    public void OrderByPriority_should_put_earlier_expressions_first_and_keep_the_order_otherwise()
    {
        string[] branches = ["feature", "release/2", "zed", "master", "main-old", "release/1", "mainline/x"];

        // Upstream's default is one expression with alternatives, so its matches keep their order.
        RefSorting.OrderByPriority(branches, name => name, InMemoryAppPreferences.DefaultPrioritizedBranchNames)
            .Should().Equal("release/2", "master", "main-old", "release/1", "feature", "zed", "mainline/x");
        RefSorting.OrderByPriority(branches, name => name, "main[^/]*;master;release/.*")
            .Should().Equal("main-old", "master", "release/2", "release/1", "feature", "zed", "mainline/x");
    }

    [Test]
    public void OrderByPriority_should_leave_the_list_alone_without_expressions_and_skip_an_invalid_one()
    {
        string[] names = ["b", "a"];

        RefSorting.OrderByPriority(names, name => name, " ; ").Should().Equal("b", "a");
        RefSorting.OrderByPriority(names, name => name, "[;a").Should().Equal("a", "b");
    }

    [Test]
    public void Build_should_list_prioritized_branches_and_remotes_first()
    {
        IReadOnlyList<BranchTreeNode> roots = BranchTree.Build(
        [
            new BranchInfo("feature", IsRemote: false, IsCurrent: false),
            new BranchInfo("master", IsRemote: false, IsCurrent: true),
            new BranchInfo("zeta/feature", IsRemote: true, IsCurrent: false),
            new BranchInfo("zeta/master", IsRemote: true, IsCurrent: false),
            new BranchInfo("alpha/x", IsRemote: true, IsCurrent: false),
            new BranchInfo("upstream/x", IsRemote: true, IsCurrent: false),
            new BranchInfo("origin/x", IsRemote: true, IsCurrent: false),
        ], new RefSorting(PrioritizedBranchNames: "master", PrioritizedRemoteNames: "origin|upstream"));

        roots[0].Children.Select(node => node.Name).Should().Equal("master", "feature");
        roots[1].Children.Select(node => node.Name).Should().Equal("origin", "upstream", "alpha", "zeta");
        roots[1].Children[3].Children.Select(node => node.Name).Should().Equal("master", "feature");
    }

    [TestCase(RevisionSortOrder.GitDefault, "")]
    [TestCase(RevisionSortOrder.AuthorDate, "--author-date-order")]
    [TestCase(RevisionSortOrder.Topology, "--topo-order")]
    public void Argument_should_be_upstreams_log_flag(RevisionSortOrder order, string expected)
    {
        RevisionSorting.Argument(order).Should().Be(expected);
    }

    [Test]
    public void The_texts_are_upstreams()
    {
        string page = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "CommandsDialogs",
            "SettingsDialog", "Pages", "SortingSettingsPage.cs"));
        string designer = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "CommandsDialogs",
            "SettingsDialog", "Pages", "SortingSettingsPage.Designer.cs"));

        foreach (string text in (string[])
                 [
                     SortingTexts.RevisionSortWarning, .. SortingTexts.PrioritizedBranchNames.Split('\n'),
                     .. SortingTexts.PrioritizedRemoteNames.Split('\n'),
                 ])
        {
            page.Should().Contain(text);
        }

        foreach (string label in (string[])
                 [
                     "Sort revisions by", "Sort branches by", "Order branches", "Prioritized branches",
                     "Prioritized remotes"
                 ])
        {
            designer.Should().Contain($".Text = \"{label}\";");
        }
    }

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
