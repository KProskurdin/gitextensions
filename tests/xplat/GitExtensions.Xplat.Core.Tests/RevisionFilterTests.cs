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
    public void Show_git_notes_should_list_the_notes_refs_commits_too()
    {
        RevisionFilter.AllBranches.ToRevisionArguments(showGitNotes: true).Should().Be(
            "--exclude=refs/stash --exclude=refs/agents/** --exclude=refs/sessions/** --exclude=refs/copilot/checkpoints/** --all");
    }

    [Test]
    public void Show_session_checkpoints_should_list_the_agent_refs_commits_too()
    {
        RevisionFilter.AllBranches.ToRevisionArguments(showSessionRefs: true).Should().Be(
            "--exclude=refs/notes/ --exclude=refs/stash --all");
        new RevisionFilter(CurrentBranchOnly: true).ToRevisionArguments(showSessionRefs: true).Should().Be("HEAD",
            "as upstream, the exclusions only apply to all branches");
    }

    [Test]
    public void Current_branch_only_should_read_from_HEAD()
    {
        new RevisionFilter(CurrentBranchOnly: true).ToRevisionArguments().Should().Be("HEAD");
    }

    [Test]
    public void ToRevisionArguments_should_stop_at_the_last_revision_to_display()
    {
        new RevisionFilter(CurrentBranchOnly: true, LastRevisionToDisplayHash: "abc123").ToRevisionArguments()
            .Should().Be("HEAD ...abc123");
    }

    [Test]
    public void Filters_should_come_in_upstream_order_and_ignore_case()
    {
        RevisionFilter filter = new(CurrentBranchOnly: true, Author: "ann", Message: "fix \"x\"",
            Since: new DateTime(2026, 1, 2),
            NoMerges: true, FirstParent: true);

        filter.ToRevisionArguments().Should().Be(
            "--since=\"2026-01-02 00:00:00\" --no-merges --author=\"ann\" --regexp-ignore-case --grep=\"fix x\" --parents --first-parent HEAD");
        filter.IsNarrowed.Should().BeTrue();
    }

    [Test]
    public void A_path_alone_narrows_the_list()
    {
        new RevisionFilter(PathFilter: "src").IsNarrowed.Should().BeTrue();
    }

    [Test]
    public void A_filter_that_may_hide_parents_should_have_git_rewrite_them_as_upstream()
    {
        new RevisionFilter(PathFilter: "src").ToRevisionArguments().Should().StartWith("--parents ",
            "the commits of a file's history connect in the graph");
        new RevisionFilter(FirstParent: true).ToRevisionArguments().Should().NotContain("--parents",
            "first parent only follows branches, it hides no parent");
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
            .Should().Be("--regexp-ignore-case --invert-grep --grep=\"wip\" --parents HEAD");
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

    [Test]
    public void ToRevisionArguments_should_follow_upstreams_FormRevisionFilter_options_and_order()
    {
        RevisionFilter filter = new(CurrentBranchOnly: true, Author: "ann", NoMerges: true, FirstParent: true,
            SimplifyByDecoration: true, IgnoreCase: false, FullHistory: true, SimplifyMerges: true);

        filter.ToRevisionArguments().Should().Be(
            "--no-merges --simplify-by-decoration --author=\"ann\" --parents --full-history --simplify-merges --first-parent HEAD",
            "ignore case off drops --regexp-ignore-case; simplify by decoration is a revision filter for the full history");
        new RevisionFilter(SimplifyByDecoration: true, FullHistory: true).ToRevisionArguments().Should()
            .StartWith("--simplify-by-decoration --parents --full-history ");
        new RevisionFilter(Message: "fix", DiffContent: "x").ToRevisionArguments().Should()
            .EndWith("--all --boundary", "upstream adds the boundary for a message and a diff filter together");
        new RevisionFilter(CommitsLimit: 5).ToRevisionArguments().Should().NotContain("--max-count",
            "the list stops reading at the limit, as each page would be limited too");
    }

    [Test]
    public void SimplifyByDecoration_should_narrow_the_list_and_be_summarized()
    {
        RevisionFilter filter = new(SimplifyByDecoration: true);

        (filter.IsNarrowed, filter.HasRevisionFilter, filter.HasFilter).Should().Be((true, true, true));
        filter.Summary(label => label).Should().Be("Simplify by decoration");
        new RevisionFilter(CommitsLimit: 5, IgnoreCase: false).HasFilter.Should().BeFalse(
            "upstream counts neither the limit nor ignore case as a filter");
        filter.ResetAll().SimplifyByDecoration.Should().BeFalse();
        new RevisionFilter(CommitsLimit: 5, FullHistory: true).ResetAll()
            .Should().Be(new RevisionFilter(CommitsLimit: 5, FullHistory: true), "upstream keeps them as settings");
    }
}
