using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RevisionFilterTests
{
    [Test]
    public void The_default_should_show_every_branch_but_notes_the_stash_and_agent_refs()
    {
        RevisionFilter.AllBranches.ToRevisionArguments().Should().Be(
            "--exclude=refs/notes/ --exclude=refs/stash --exclude=refs/agents/** --exclude=refs/sessions/** --exclude=refs/copilot/checkpoints/** --all");
        RevisionFilter.AllBranches.IsNarrowed.Should().BeFalse();
    }

    [Test]
    public void Current_branch_only_should_read_from_HEAD()
    {
        new RevisionFilter(CurrentBranchOnly: true).ToRevisionArguments().Should().Be("HEAD");
    }

    [Test]
    public void Filters_should_come_in_upstream_order_and_ignore_case()
    {
        RevisionFilter filter = new(CurrentBranchOnly: true, Author: "ann", Message: "fix \"x\"",
            Since: new DateTime(2026, 1, 2),
            NoMerges: true, FirstParent: true);

        filter.ToRevisionArguments().Should().Be(
            "--since=\"2026-01-02 00:00:00\" --no-merges --author=\"ann\" --regexp-ignore-case --grep=\"fix x\" --first-parent HEAD");
        filter.IsNarrowed.Should().BeTrue();
    }

    [Test]
    public void A_path_alone_narrows_the_list()
    {
        new RevisionFilter(PathFilter: "src").IsNarrowed.Should().BeTrue();
    }

    [Test]
    public void Diff_content_should_search_with_G_before_the_message_as_upstream()
    {
        RevisionFilter filter = new(Message: "fix", DiffContent: "Foo(");

        filter.ToRevisionArguments().Should().StartWith("--regexp-ignore-case -G\"Foo(\" --grep=\"fix\" ");
        filter.IsNarrowed.Should().BeTrue();
    }

    [Test]
    public void A_message_starting_with_dashes_should_be_passed_as_git_log_options()
    {
        new RevisionFilter(CurrentBranchOnly: true, Message: " --invert-grep --grep=\"wip\"").ToRevisionArguments()
            .Should().Be("--regexp-ignore-case --invert-grep --grep=\"wip\" HEAD");
    }

    [Test]
    public void Filtered_branches_should_replace_all_with_the_branch_filter_words()
    {
        RevisionFilter filter = new(ShowFilteredBranches: true, BranchFilter: " main feature/* --tags a..b ",
            ShowReflog: true);

        filter.ToRevisionArguments().Should().Be("--reflog main --branches=feature/* --tags a..b");
        filter.ShowsFilteredBranches.Should().BeTrue();
        filter.HasFilter.Should().BeTrue();
        filter.IsNarrowed.Should().BeFalse();
    }

    [Test]
    public void Filtered_branches_without_a_filter_or_with_current_branch_only_should_not_apply()
    {
        new RevisionFilter(ShowFilteredBranches: true).ToRevisionArguments().Should().EndWith("--all");
        new RevisionFilter(ShowFilteredBranches: true, BranchFilter: "main", CurrentBranchOnly: true)
            .ToRevisionArguments()
            .Should().Be("HEAD");
    }

    [Test]
    public void ResetAll_should_keep_only_the_current_branch_and_reflog_choices()
    {
        RevisionFilter filter = new(CurrentBranchOnly: true, Author: "ann", FirstParent: true,
            ShowFilteredBranches: true,
            BranchFilter: "main", ShowReflog: true);

        filter.ResetAll().Should().Be(new RevisionFilter(CurrentBranchOnly: true, ShowReflog: true));
    }

    [Test]
    public void Summary_should_list_the_filters_with_upstreams_labels()
    {
        RevisionFilter filter = new(Author: "ann", DiffContent: "x", FirstParent: true, ShowFilteredBranches: true,
            BranchFilter: "main");

        filter.Summary(label => $"[{label}]").Should().Be(string.Join(Environment.NewLine,
            "[Author]: ann", "[Diff contains]: x", "[Show only first parent]", "[Branches]: main"));
        RevisionFilter.AllBranches.Summary(label => label).Should().BeEmpty();
    }
}
