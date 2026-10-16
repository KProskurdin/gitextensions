using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class GridLabelsTests
{
    private static readonly GridViewOptions _all = new();

    [Test]
    public void Layout_should_leave_out_HEAD_and_sort_current_local_remote_then_tags()
    {
        IReadOnlyList<GridLabel> layout = GridLabels.Layout(
        [
            new RefLabel("HEAD", RefKind.Head),
            new RefLabel("v1", RefKind.Tag),
            new RefLabel("upstream/x", RefKind.RemoteBranch) { Remote = "upstream" },
            new RefLabel("b", RefKind.Branch),
            new RefLabel("main", RefKind.Branch) { IsCurrent = true },
        ], _all);

        layout.Select(label => (label.Text, label.Shape, label.IsCurrent)).Should().Equal(
            ("main", GridLabelShape.Rect, true),
            ("b", GridLabelShape.Rect, false),
            ("upstream/x", GridLabelShape.Rect, false),
            ("v1", GridLabelShape.PointLeft, false));
    }

    [Test]
    public void Layout_should_pair_a_branch_with_the_remote_branch_it_tracks_on_the_same_commit()
    {
        IReadOnlyList<GridLabel> layout = GridLabels.Layout(
        [
            new RefLabel("origin/main", RefKind.RemoteBranch) { Remote = "origin" },
            new RefLabel("main", RefKind.Branch) { IsCurrent = true, Upstream = "origin/main", AheadBehind = "↓↑" },
        ], _all);

        GridLabel pair = layout.Should().ContainSingle().Which;
        pair.Shape.Should().Be(GridLabelShape.PointRight);
        pair.Nestled!.Should().Match<GridLabel>(remote =>
            remote.Text == "origin" && remote.Shape == GridLabelShape.NotchLeft && remote.Kind == RefKind.RemoteBranch);
    }

    [Test]
    public void Layout_should_add_the_ahead_and_behind_arrows_of_a_branch_tracked_elsewhere()
    {
        IReadOnlyList<GridLabel> layout = GridLabels.Layout(
        [
            new RefLabel("feature", RefKind.Branch) { Upstream = "origin/feature", AheadBehind = "↓" },
            new RefLabel("origin/other", RefKind.RemoteBranch) { Remote = "origin", AheadBehind = "↑" },
        ], _all);

        layout[0].Nestled!.Should().Match<GridLabel>(arrows =>
            arrows.Text == "↓" && arrows.Shape == GridLabelShape.NotchLeft && arrows.Ref == null);
        layout[1].Shape.Should().Be(GridLabelShape.NotchRight);
        layout[1].Nestled!.Shape.Should().Be(GridLabelShape.PointLeft);
    }

    [Test]
    public void Layout_should_shorten_a_remote_branch_named_as_the_single_local_branch_to_its_remote()
    {
        IReadOnlyList<GridLabel> layout = GridLabels.Layout(
        [
            new RefLabel("main", RefKind.Branch) { Upstream = "origin/main" },
            new RefLabel("origin/main", RefKind.RemoteBranch) { Remote = "origin" },
            new RefLabel("upstream/main", RefKind.RemoteBranch) { Remote = "upstream" },
        ], _all);

        layout.Select(label => label.Text).Should().Equal("main", "upstream");
    }

    [Test]
    public void Layout_should_name_the_remote_whose_color_a_label_takes()
    {
        IReadOnlyList<GridLabel> layout = GridLabels.Layout(
        [
            new RefLabel("main", RefKind.Branch) { Upstream = "origin/main", TrackingRemote = "origin" },
            new RefLabel("origin/main", RefKind.RemoteBranch) { Remote = "origin" },
            new RefLabel("feature", RefKind.Branch) { Upstream = "fork/feature", TrackingRemote = "fork", AheadBehind = "↑" },
            new RefLabel("fork/other", RefKind.RemoteBranch) { Remote = "fork" },
            new RefLabel("v1", RefKind.Tag),
        ], _all);

        layout.Select(label => (label.Text, label.Remote, label.Nestled?.Remote)).Should().Equal(
            ("feature", null, "fork"),
            ("main", null, "origin"),
            ("fork/other", "fork", null),
            ("v1", null, null));
    }

    [Test]
    public void Layout_should_hide_tags_and_remote_branches_as_the_view_menu_says()
    {
        RefLabel[] labels = [new("v1", RefKind.Tag), new("origin/x", RefKind.RemoteBranch), new("x", RefKind.Branch)];

        GridLabels.Layout(labels, new GridViewOptions(ShowTags: false, ShowRemoteBranches: false))
            .Select(label => label.Text).Should().Equal("x");
    }
}
