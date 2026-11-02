using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class GridSelectionTests
{
    private static readonly CommitRow _a = Row("a", "pa");
    private static readonly CommitRow _b = Row("b", "pb");
    private static readonly CommitRow _c = Row("c");

    [Test]
    public void Update_should_put_the_latest_selected_first()
    {
        GridSelection selection = new();

        selection.Update([_b], [_b]);
        selection.Update([_a, _b], [_a]);
        selection.Update([_a, _b, _c], [_c]);

        selection.Revisions.Should().Equal(_c, _a, _b);
        selection.Latest.Should().Be(_c);
    }

    [Test]
    public void Update_should_drop_rows_no_longer_selected_and_keep_rows_selected_without_notice()
    {
        GridSelection selection = new();
        selection.Update([_a, _b], [_a, _b]);

        selection.Update([_b, _c], []);

        selection.Revisions.Should()
            .Equal([_c, _b], "a row the grid selected without notice joins after the known ones");
        selection.Update([_c], [_c]);
        selection.Revisions.Should().Equal(_c);
    }

    [Test]
    public void FirstAndSelected_should_take_the_first_parent_of_one_commit_and_the_earliest_of_several()
    {
        GridSelection.FirstAndSelected([]).Should().Be(((string?)null, (CommitRow?)null));
        GridSelection.FirstAndSelected([_a]).Should().Be(("pa", _a));
        GridSelection.FirstAndSelected([_c]).Should().Be(((string?)null, _c), "a root commit has no parent");
        GridSelection.FirstAndSelected([_c, _a, _b]).Should().Be(("b", _c));
    }

    [Test]
    public void CopyItems_should_join_the_values_of_several_commits()
    {
        IReadOnlyList<GridCopyItem?> items = GridMenus.CopyItems([_a, _b], "Branches", "Tags");

        GridCopyItem hashes = items.OfType<GridCopyItem>().First(item => item.Image == "CommitId");
        hashes.Value.Should().Be("a\nb");
        hashes.Text.Should().EndWith("a, b");
        items.OfType<GridCopyItem>().Count(item => item.Image == "Date").Should()
            .Be(2, "several commits get the author and the commit dates");
    }

    [Test]
    public async Task ToBeSelectedAsync_should_keep_the_selected_commits_that_are_still_shown()
    {
        IReadOnlyList<string> select = await GridSelection.ToBeSelectedAsync(["a", "gone", "c"], [_a, _b, _c], NoAncestors);

        select.Should().Equal("a", "c");
    }

    [Test]
    public async Task ToBeSelectedAsync_should_select_HEAD_when_nothing_was_selected()
    {
        CommitRow head = Row("h") with { Labels = [new RefLabel("HEAD", RefKind.Head)] };

        IReadOnlyList<string> select = await GridSelection.ToBeSelectedAsync([], [_a, head], NoAncestors);

        select.Should().Equal("h");
    }

    [Test]
    public async Task ToBeSelectedAsync_should_select_the_nearest_shown_ancestor_of_a_commit_no_longer_shown()
    {
        IReadOnlyList<string> select = await GridSelection.ToBeSelectedAsync(["gone"], [_a, _b, _c],
            hash => Task.FromResult<IReadOnlyList<string>>(hash == "gone" ? ["gone", "x", "b", "a"] : []));

        select.Should().Equal("b");
    }

    [Test]
    public async Task ToBeSelectedAsync_should_select_nothing_for_an_artificial_row_no_longer_shown()
    {
        bool asked = false;

        IReadOnlyList<string> select = await GridSelection.ToBeSelectedAsync([ArtificialCommits.WorkTreeHash], [_a],
            _ =>
            {
                asked = true;
                return Task.FromResult<IReadOnlyList<string>>(["a"]);
            });

        select.Should().BeEmpty();
        asked.Should().BeFalse("upstream does not look for the parents of an artificial commit");
    }

    private static Task<IReadOnlyList<string>> NoAncestors(string hash) => Task.FromResult<IReadOnlyList<string>>([]);

    private static CommitRow Row(string hash, string? parent = null)
        => new(hash, hash, "subject " + hash, "author", "", parent is null ? [] : [parent]);
}
