using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class QuickSearchTests
{
    private static readonly IReadOnlyList<CommitRow> _rows =
    [
        new("aaa1111", "aaa1111", "Fix the parser", "Ann", "", Labels: [new RefLabel("main", RefKind.Branch)]),
        new("bbb2222", "bbb2222", "Add tests", "Bob", ""),
        new("ccc3333", "ccc3333", "Fix the build", "Cid", ""),
    ];

    [Test]
    public void Typing_finds_the_next_match_from_the_selected_row_and_wraps_around()
    {
        QuickSearch search = new();

        search.Type("F", _rows, current: 1).Should().Be(2);
        search.Text.Should().Be("Searching for: f");
        search.Next(_rows, current: 2, down: true).Should().Be(0, "the search goes on from the top");
        search.Next(_rows, current: 0, down: false).Should().Be(2);
    }

    [Test]
    public void Upstreams_matcher_looks_at_refs_hashes_authors_and_messages()
    {
        QuickSearch.Matches(_rows[0], "mai").Should().BeTrue("a ref name contains it");
        QuickSearch.Matches(_rows[1], "bbb2").Should().BeTrue("the hash starts with it");
        QuickSearch.Matches(_rows[1], "bb").Should().BeFalse("a hash needs three characters");
        QuickSearch.Matches(_rows[2], "cid").Should().BeTrue("the author contains it");
        QuickSearch.Matches(_rows[1], "nothing").Should().BeFalse();
    }

    [Test]
    public void A_search_without_a_match_is_marked_and_backspace_keeps_one_character()
    {
        QuickSearch search = new();

        search.Type("z", _rows, current: 0).Should().BeNull();
        search.Found.Should().BeFalse();
        search.Backspace(_rows, current: 0).Should().BeNull("one character is left, as upstream");
        search.IsActive.Should().BeTrue();
        search.End();
        search.IsActive.Should().BeFalse();
    }
}
