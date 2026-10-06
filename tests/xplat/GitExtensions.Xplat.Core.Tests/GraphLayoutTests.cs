using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class GraphLayoutTests
{
    [Test]
    public void Compute_should_keep_a_linear_history_in_one_lane()
    {
        IReadOnlyList<GraphRow> graph = GraphLayout.Compute([Commit("a", "b"), Commit("b", "c"), Commit("c")]);

        graph.Select(row => row.Column).Should().Equal(0, 0, 0);
        graph[0].ParentColumns.Should().Equal(0);
        graph[2].ParentColumns.Should().BeEmpty();
        graph.Max(row => row.Width).Should().Be(1);
    }

    [Test]
    public void Compute_should_open_a_second_lane_for_the_second_parent_of_a_merge()
    {
        IReadOnlyList<GraphRow> graph = GraphLayout.Compute(
            [Commit("m", "a", "b"), Commit("a", "c"), Commit("b", "c"), Commit("c")]);

        graph[0].Column.Should().Be(0);
        graph[0].ParentColumns.Should().Equal(0, 1);
        graph[0].Width.Should().Be(2);
    }

    [Test]
    public void Compute_should_join_the_lane_of_a_parent_that_another_branch_already_continues()
    {
        IReadOnlyList<GraphRow> graph = GraphLayout.Compute(
            [Commit("m", "a", "b"), Commit("a", "c"), Commit("b", "c"), Commit("c")]);

        graph[2].Column.Should().Be(1);
        graph[2].ParentColumns.Should().Equal(0);
        graph[3].Column.Should().Be(0);
    }

    [Test]
    public void Compute_should_send_a_second_child_into_the_lane_its_parent_already_has()
    {
        IReadOnlyList<GraphRow> graph = GraphLayout.Compute([Commit("x", "p"), Commit("y", "p"), Commit("p")]);

        graph[1].Column.Should().Be(1);
        graph[1].ParentColumns.Should().Equal(0);
        graph[2].Column.Should().Be(0);
    }

    [Test]
    public void Compute_should_report_the_lanes_open_above_each_commit()
    {
        IReadOnlyList<GraphRow> graph = GraphLayout.Compute(
            [Commit("m", "a", "b"), Commit("a", "c"), Commit("b", "c"), Commit("c")]);

        graph[0].ActiveColumns.Should().BeEmpty();
        graph[2].ActiveColumns.Should().Equal(0, 1);
        graph[3].ActiveColumns.Should().Equal(0);
    }

    [Test]
    public void Compute_should_keep_one_color_along_a_lane()
    {
        IReadOnlyList<GraphRow> graph = GraphLayout.Compute(
            [Commit("m", "a", "b"), Commit("a", "c"), Commit("b", "c"), Commit("c")]);

        graph[1].Color.Should().Be(graph[0].Color);
        graph[3].Color.Should().Be(graph[0].Color);
        graph[2].Color.Should().NotBe(graph[0].Color, "the merged branch has its own lane");
        graph[0].ParentColorAt(1).Should().Be(graph[2].Color);
    }

    [Test]
    public void Compute_should_give_the_line_into_another_lane_the_color_of_the_branch_that_joins()
    {
        IReadOnlyList<GraphRow> graph = GraphLayout.Compute([Commit("x", "p"), Commit("y", "p"), Commit("p")]);

        graph[1].ParentColumns.Should().Equal(0);
        graph[1].ParentColorAt(0).Should().Be(graph[1].Color);
        graph[1].Color.Should().NotBe(graph[0].Color);
    }

    [Test]
    public void Compute_should_report_the_colors_of_the_lanes_that_pass_through()
    {
        IReadOnlyList<GraphRow> graph = GraphLayout.Compute(
            [Commit("m", "a", "b"), Commit("a", "c"), Commit("b", "c"), Commit("c")]);

        graph[1].ActiveColumns.Should().Equal(0, 1);
        graph[1].ActiveColorAt(1).Should().Be(graph[2].Color);
    }

    [Test]
    public void Compute_should_lay_out_5000_commits_with_merges_quickly()
    {
        List<CommitRow> rows = [];
        const int count = 5000;
        for (int i = 0; i < count; i++)
        {
            // Every tenth commit merges a short side branch, as a busy history does.
            string parent = $"c{i + 1}";
            rows.Add(i % 10 == 0 && i + 5 < count
                ? Commit($"c{i}", parent, $"c{i + 5}")
                : Commit($"c{i}", i + 1 < count ? [parent] : []));
        }

        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        IReadOnlyList<GraphRow> graph = GraphLayout.Compute(rows);
        watch.Stop();

        graph.Should().HaveCount(count);
        graph.Max(row => row.Width).Should().BeLessThan(4);
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Test]
    public void Compute_should_return_no_rows_for_no_commits()
    {
        GraphLayout.Compute([]).Should().BeEmpty();
    }

    private static CommitRow Commit(string hash, params string[] parents) =>
        new(hash, hash, "subject " + hash, "author", "2026-10-04 12:00", parents);
}
