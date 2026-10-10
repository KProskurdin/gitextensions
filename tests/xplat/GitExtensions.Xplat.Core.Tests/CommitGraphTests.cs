using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// Upstream's graph reads its three settings from the user's settings; these tests hold for every combination of them.
internal sealed class CommitGraphTests
{
    private const int RowHeight = 22;

    [Test]
    public void A_linear_history_is_one_lane_in_gits_order()
    {
        CommitRow[] rows = [Row("c", "b"), Row("b", "a"), Row("a")];

        CommitGraph graph = CommitGraph.Build(rows);

        graph.OrderedRows.Should().Equal(rows);
        graph.LaneCount.Should().Be(1);
        IReadOnlyList<GraphShape> middle = GraphPainter.Paint(graph, 1, RowHeight, hasRefs: false, isHead: false);
        middle.OfType<GraphLine>().Should().NotBeEmpty()
            .And.OnlyContain(line =>
                line.From.X == GraphPainter.LaneWidth / 2 && line.To.X == GraphPainter.LaneWidth / 2);
        middle.OfType<GraphNode>().Should().ContainSingle().Which.Square.Should().BeFalse();
    }

    [Test]
    public void A_merged_side_branch_gets_its_own_lane_and_color()
    {
        CommitGraph graph = CommitGraph.Build([Row("m", "a", "s"), Row("s", "b"), Row("a", "b"), Row("b")]);
        graph.DrawStyle = GraphDrawStyle.Normal;

        graph.LaneCount.Should().Be(2);
        GraphNode side = Node(graph, graph.OrderedRows.ToList().FindIndex(row => row.Hash == Hash("s")));
        GraphNode main = Node(graph, graph.OrderedRows.ToList().FindIndex(row => row.Hash == Hash("a")));
        side.Bounds.X.Should().BeGreaterThan(main.Bounds.X);
        side.Color.Should().NotBe(main.Color);

        // The merge row leaves its node towards the side lane: a curve or a diagonal, not only straight down.
        GraphPainter.Paint(graph, 0, RowHeight, hasRefs: false, isHead: false)
            .Where(shape => shape is GraphBezier || (shape is GraphLine line && line.From.X != line.To.X))
            .Should().NotBeEmpty();
    }

    [Test]
    public void Non_relatives_are_gray_until_a_branch_or_a_hover_highlights_them()
    {
        // HEAD is "a" on the main line; "s" is a side branch that is not one of its ancestors.
        CommitGraph graph = CommitGraph.Build(
            [Row("s", "b"), Row("a", "b") with { Labels = [new RefLabel("HEAD", RefKind.Head)] }, Row("b")]);
        int side = graph.OrderedRows.ToList().FindIndex(row => row.Hash == Hash("s"));
        int main = graph.OrderedRows.ToList().FindIndex(row => row.Hash == Hash("a"));

        graph.DrawStyle.Should().Be(GraphDrawStyle.DrawNonRelativesGray, "upstream's default");
        Node(graph, side).Color.Should().Be(GraphPainter.NonRelativeColor);
        Node(graph, main).Color.Should().NotBe(GraphPainter.NonRelativeColor);

        graph.HoverHighlighted = graph.AncestryOf(side);
        Node(graph, side).Color.Should().NotBe(GraphPainter.NonRelativeColor);
        Node(graph, main).Color.Should().Be(GraphPainter.NonRelativeColor, "the hover shows only the hovered ancestry");
        graph.HoverHighlighted = null;

        graph.HighlightBranch(Hash("s"));
        graph.DrawStyle.Should().Be(GraphDrawStyle.HighlightSelected);
        Node(graph, side).Color.Should().NotBe(GraphPainter.NonRelativeColor);
        Node(graph, main).Color.Should().Be(GraphPainter.NonRelativeColor);
    }

    [Test]
    public void The_shown_width_is_the_page_until_the_view_narrows_it_to_its_rows()
    {
        CommitGraph graph = CommitGraph.Build([Row("m", "a", "s"), Row("s", "b"), Row("a", "b"), Row("b"), Row("c")]);

        graph.ShownLaneCount.Should().Be(graph.LaneCount);
        graph.LaneCountAt(graph.OrderedRows.ToList().FindIndex(row => row.Hash == Hash("s"))).Should().Be(2);
        graph.ShownLaneCount = 1;
        graph.ShownLaneCount.Should().Be(1);
    }

    [Test]
    public void A_lanes_tooltip_names_its_commit_branch_and_message_as_upstream()
    {
        CommitGraph graph = CommitGraph.Build(
        [
            Row("m", "a", "s") with { Subject = "Merge branch 'feature' into main" },
            Row("s", "b") with { Subject = "side work" },
            Row("a", "b") with { Subject = "main work", Labels = [new RefLabel("main", RefKind.Branch)] },
            Row("b") with { Subject = "base" },
        ]);
        int side = graph.OrderedRows.ToList().FindIndex(row => row.Hash == Hash("s"));
        int sideLane = (int)(Node(graph, side).Bounds.X / GraphPainter.LaneWidth);

        string info = graph.LaneInfo(side, sideLane);

        info.Should().StartWith("* " + Hash("s"));
        info.Should().Contain("Branch: feature");
        info.Should().EndWith("side work");
        graph.LaneInfo(side, lane: 5).Should().BeEmpty();
    }

    [Test]
    public void A_parent_listed_before_its_child_is_shown_after_it_as_upstreams_grid_does()
    {
        CommitGraph graph = CommitGraph.Build([Row("a"), Row("b", "a")]);

        graph.OrderedRows.Select(row => row.Hash).Should().Equal(Hash("b"), Hash("a"));
    }

    [Test]
    public void A_commit_whose_parent_is_not_loaded_is_drawn_without_a_lane_to_it()
    {
        // As upstream's renderer: a segment to a commit that is not a row is not drawn.
        CommitGraph graph = CommitGraph.Build([Row("b", "a")]);

        IReadOnlyList<GraphShape> shapes = GraphPainter.Paint(graph, 0, RowHeight, hasRefs: false, isHead: false);
        shapes.OfType<GraphNode>().Should().ContainSingle();
        shapes.OfType<GraphLine>().Should().BeEmpty();
    }

    [Test]
    public void Nodes_with_refs_are_squares_and_the_checked_out_one_is_outlined()
    {
        CommitRow head = Row("b", "a") with
        {
            Labels = [new RefLabel("HEAD", RefKind.Head), new RefLabel("main", RefKind.Branch)]
        };
        CommitGraph graph = CommitGraph.Build([head, Row("a")]);

        GraphRowRef cell = graph.RowAt(0);
        (cell.HasRefs, cell.IsHead).Should().Be((true, true));
        GraphNode node = GraphPainter.Paint(graph, 0, RowHeight, cell.HasRefs, cell.IsHead).OfType<GraphNode>()
            .Single();
        (node.Square, node.Outline).Should().Be((true, true));
        node.Bounds.Width.Should().Be(GraphPainter.NodeDimension);
    }

    [Test]
    public void The_settings_and_sizes_are_upstreams()
    {
        string root = RepositoryRoot();
        string settings =
            File.ReadAllText(Path.Combine(root, "src", "app", "GitCommands", "Settings", "AppSettings.cs"));
        string page = File.ReadAllText(Path.Combine(root, "src", "app", "GitUI", "CommandsDialogs", "SettingsDialog",
            "Pages", "DetailedSettingsPage.Designer.cs"));
        string renderer = File.ReadAllText(Path.Combine(root, "src", "app", "GitUI", "UserControls", "RevisionGrid",
            "Graph", "Rendering", "GraphRenderer.cs"));

        foreach (string name in (string[])
                 ["MergeGraphLanesHavingCommonParent", "RenderGraphWithDiagonals", "StraightenGraphDiagonals"])
        {
            settings.Should().Contain($"nameof({name}), true)");
        }

        foreach (string label in (string[])
                 [
                     "Merge graph lanes having common parent", "Render graph with diagonals",
                     "Straighten graph diagonals",
                 ])
        {
            page.Should().Contain($".Text = \"{label}\";");
        }

        renderer.Should().Contain($"LaneLineWidth = DpiUtil.Scale({GraphPainter.LaneLineWidth})")
            .And.Contain($"LaneWidth = DpiUtil.Scale({GraphPainter.LaneWidth})")
            .And.Contain($"NodeDimension = DpiUtil.Scale({GraphPainter.NodeDimension})");
    }

    private static GraphNode Node(CommitGraph graph, int index)
        => GraphPainter.Paint(graph, index, RowHeight, hasRefs: false, isHead: false).OfType<GraphNode>().Single();

    private static CommitRow Row(string name, params string[] parents)
        => new(Hash(name), name, name, "Ann", "", [.. parents.Select(Hash)]);

    // A stable 40-digit hex hash per name.
    private static string Hash(string name)
        => Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(name)))
            .ToLowerInvariant();

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
