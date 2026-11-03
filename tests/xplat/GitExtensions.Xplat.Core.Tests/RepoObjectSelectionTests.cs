using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RepoObjectSelectionTests
{
    [Test]
    public void Click_should_mark_the_node_alone_and_with_ctrl_toggle_it_with_shift_its_descendants_too()
    {
        IReadOnlyList<RepoObjectNode> roots = Roots();
        RepoObjectNode main = Node(roots, "main");
        RepoObjectNode folder = Node(roots, "feature");

        RepoObjectSelection.Click(roots, main, rightButton: false, multiple: false, includingDescendants: false);
        RepoObjectSelection.Selected(roots).Should().Equal(main);

        RepoObjectSelection.Click(roots, folder, rightButton: false, multiple: true, includingDescendants: true);
        RepoObjectSelection.Selected(roots).Select(Name).Should().Equal("main", "feature", "feature/a", "feature/b");

        RepoObjectSelection.Click(roots, Node(roots, "feature/a"), rightButton: false, multiple: true, includingDescendants: false);
        RepoObjectSelection.Selected(roots).Select(Name).Should().Equal("main", "feature", "feature/b");

        RepoObjectSelection.Click(roots, Node(roots, "feature/b"), rightButton: true, multiple: false, includingDescendants: false);
        RepoObjectSelection.Selected(roots).Should().HaveCount(3, "a right click on a marked node keeps the marks for the menu");

        RepoObjectSelection.Click(roots, Node(roots, "v1"), rightButton: true, multiple: false, includingDescendants: false);
        RepoObjectSelection.Selected(roots).Select(Name).Should().Equal(["v1"], "any other click marks the node alone");
    }

    [Test]
    public void Keep_should_mark_the_same_nodes_of_a_refilled_tree()
    {
        IReadOnlyList<RepoObjectNode> roots = Roots();
        RepoObjectSelection.Select(roots, Node(roots, "main"), multiple: true, includingDescendants: false);
        RepoObjectSelection.Select(roots, Node(roots, "v1"), multiple: true, includingDescendants: false);
        IReadOnlyList<RepoObjectNode> refilled = Roots();

        RepoObjectSelection.Keep(roots, refilled);

        RepoObjectSelection.Selected(refilled).Select(Name).Should().Equal("main", "v1");
        RepoObjectSelection.Selected(refilled).Should().OnlyContain(node => Node(refilled, Name(node)) == node);
    }

    [Test]
    public void RefsFilter_should_join_the_branches_and_tags_among_the_nodes()
    {
        IReadOnlyList<RepoObjectNode> roots = Roots();

        RepoObjectSelection.RefsFilter([Node(roots, "main"), Node(roots, "feature"), Node(roots, "origin/main"), Node(roots, "v1")])
            .Should().Be("main origin/main v1");
        RepoObjectSelection.RefsFilter([Node(roots, "feature"), roots[0]]).Should().BeNull();
    }

    private static string Name(RepoObjectNode node)
        => node switch
        {
            { Branch: { } branch } => branch.Name,
            { Item: string item } => item,
            _ => node.Text,
        };

    private static RepoObjectNode Node(IReadOnlyList<RepoObjectNode> roots, string name)
        => roots.SelectMany(root => root.Descendants()).First(node => !node.IsRoot && Name(node) == name);

    private static IReadOnlyList<RepoObjectNode> Roots()
        => RepoObjectsTree.Build(
            new RepoObjects(
                [
                    new BranchInfo("main", IsRemote: false, IsCurrent: true),
                    new BranchInfo("feature/a", IsRemote: false, IsCurrent: false),
                    new BranchInfo("feature/b", IsRemote: false, IsCurrent: false),
                    new BranchInfo("origin/main", IsRemote: true, IsCurrent: false),
                ],
                ["origin"],
                ["v1"],
                [],
                null,
                [],
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                "/work/repo"),
            RepoObjectsTree.Roots,
            text => text);
}
