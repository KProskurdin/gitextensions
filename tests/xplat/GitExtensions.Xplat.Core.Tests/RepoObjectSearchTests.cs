using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RepoObjectSearchTests
{
    [Test]
    public void Next_should_go_breadth_first_from_the_roots_highlight_every_match_and_wrap_around()
    {
        IReadOnlyList<RepoObjectNode> roots = Roots();
        RepoObjectSearch search = new();

        RepoObjectNode? first = search.Next(roots, "MAIN", out bool focus);

        focus.Should().BeFalse();
        first.Should().BeSameAs(Branch(roots, "main"));
        search.Results.Select(Describe).Should().Equal("feature/main-fix", "origin/main", "main");
        Nodes(roots).Where(node => node.IsSearchMatch).Should().HaveCount(3);
        search.Next(roots, "MAIN", out _).Should().BeSameAs(Branch(roots, "feature/main-fix"),
            "the new shell goes on to the second match where upstream selects the first again");
        search.Next(roots, "MAIN", out _).Should().BeSameAs(Branch(roots, "origin/main"));
        search.Next(roots, "MAIN", out _).Should().BeSameAs(first);
    }

    [Test]
    public void Next_should_match_refs_by_full_path_and_the_other_nodes_by_their_text()
    {
        IReadOnlyList<RepoObjectNode> roots = Roots();

        Matches(roots, "feature/").Should().Equal("feature/main-fix");
        Matches(roots, "origin/f").Should().Equal(["feat", "origin/feat/y"],
            "a remote branch's folder starts with its remote's name, as upstream's");
        Matches(roots, "stash@").Should().Equal("stash@{0}");
        Matches(roots, "WIP").Should().BeEmpty("upstream matches a stash by its selector, not its message");
        Matches(roots, "anch").Should().Equal(["Branches"], "a root matches by its text");
        Matches(roots, "other").Should().Equal("other");
        Matches(roots, " ").Should().BeEmpty();
    }

    [Test]
    public void Next_should_start_over_and_clear_the_highlighting_once_the_text_changed()
    {
        IReadOnlyList<RepoObjectNode> roots = Roots();
        RepoObjectSearch search = new();
        search.Next(roots, "main", out _);

        search.CriteriaChanged();
        RepoObjectNode? next = search.Next(roots, "v1", out bool focus);

        focus.Should().BeFalse();
        next!.Item.Should().Be("v1");
        Nodes(roots).Where(node => node.IsSearchMatch).Should().ContainSingle().Which.Should().BeSameAs(next);

        search.CriteriaChanged();
        search.Next(roots, "", out focus).Should().BeNull();

        focus.Should().BeTrue("upstream focuses the search box after an emptied search");
        Nodes(roots).Should().OnlyContain(node => !node.IsSearchMatch);
        search.Next(roots, "nothing", out focus).Should().BeNull();
        focus.Should().BeFalse();
    }

    [Test]
    public void Remap_should_carry_the_highlighting_over_to_a_refilled_tree()
    {
        RepoObjectSearch search = new();
        search.Next(Roots(), "main", out _);
        IReadOnlyList<RepoObjectNode> refilled = Roots(withMain: false);

        search.Remap(refilled);

        search.Results.Select(Describe).Should().Equal("feature/main-fix", "origin/main");
        search.Results.Should().OnlyContain(node => node.IsSearchMatch && Nodes(refilled).Contains(node));
        search.Next(refilled, "main", out _).Should().BeSameAs(Branch(refilled, "feature/main-fix"));
    }

    [Test]
    public void Candidates_should_list_leaf_refs_by_full_path_and_other_nodes_by_text_below_the_roots()
    {
        RepoObjectSearch.Candidates(Roots()).Should().Equal(
            "main", "feature/main-fix",
            "origin/main", "origin/feat/y", "upstream",
            "repo", "other",
            "v1",
            "lib",
            "stash@{0}");
    }

    private static IReadOnlyList<string> Matches(IReadOnlyList<RepoObjectNode> roots, string text)
    {
        RepoObjectSearch search = new();
        search.Next(roots, text, out _);
        return [.. search.Results.Select(Describe).Order(StringComparer.Ordinal)];
    }

    // The node's searched name, for the assertions.
    private static string Describe(RepoObjectNode node)
        => node switch
        {
            { Branch: { } branch } => branch.Name,
            { Kind: RepoObjectKind.Stash, Item: StashInfo stash } => stash.Name,
            { Kind: RepoObjectKind.Folder, Item: string path } => path,
            _ => node.Text,
        };

    private static List<RepoObjectNode> Nodes(IReadOnlyList<RepoObjectNode> roots)
        => [.. roots.SelectMany(root => root.Descendants())];

    private static RepoObjectNode Branch(IReadOnlyList<RepoObjectNode> roots, string name)
        => Nodes(roots).Single(node => node.Branch?.Name == name);

    private static IReadOnlyList<RepoObjectNode> Roots(bool withMain = true)
    {
        List<BranchInfo> branches =
        [
            new("feature/main-fix", IsRemote: false, IsCurrent: false),
            new("origin/main", IsRemote: true, IsCurrent: false),
            new("origin/feat/y", IsRemote: true, IsCurrent: false),
        ];
        if (withMain)
        {
            branches.Insert(0, new BranchInfo("main", IsRemote: false, IsCurrent: true));
        }

        RepoObjects objects = new(
            branches,
            ["origin", "upstream"],
            ["v1"],
            [new StashInfo("stash@{0}", "WIP on main")],
            [new SubmoduleInfo("lib", IsInitialized: true, IsUpToDate: true)],
            [
                new WorktreeInfo("/work/repo", "repo", IsMain: true, IsDeleted: false),
                new WorktreeInfo("/work/other", "other", IsMain: false, IsDeleted: false),
            ],
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            "/work/repo");
        return RepoObjectsTree.Build(objects, RepoObjectsTree.Roots, text => text);
    }
}
