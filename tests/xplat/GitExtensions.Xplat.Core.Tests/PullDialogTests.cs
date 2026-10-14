using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class PullDialogTests
{
    private static readonly string[] _remotes = ["origin", "upstream"];

    [Test]
    public void The_texts_are_upstreams()
    {
        string root = Path.GetFullPath(Path.Combine(TestFolder(), "..", "..", ".."));
        string form = File.ReadAllText(Path.Combine(root, "src", "app", "GitUI", "CommandsDialogs", "FormPull.cs"))
            .Replace("\" +\r\n", "\" +\n").Replace("\" + Environment.NewLine +\n", "\\n\" +\n");
        string strings = File.ReadAllText(Path.Combine(root, "src", "app", "GitUI", "TranslatedStrings.cs"));
        foreach (FieldInfo field in typeof(PullDialog).GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(field => field.FieldType == typeof(string)))
        {
            string text = (string)field.GetValue(null)!;
            string source = field.Name is nameof(PullDialog.NotOnBranchCaption) or nameof(PullDialog.ContinueText)
                ? strings
                : form;
            foreach (string line in text.Split(Environment.NewLine).Where(line => line.Length > 0))
            {
                source.Should().Contain(line.Replace("\"", "\\\""), field.Name);
            }
        }
    }

    [TestCase(GitPullAction.None, GitPullAction.Merge, PullAction.Merge, "origin")]
    [TestCase(GitPullAction.None, GitPullAction.Rebase, PullAction.Rebase, "origin")]
    [TestCase(GitPullAction.Fetch, GitPullAction.Merge, PullAction.FetchOnly, "origin")]
    [TestCase(GitPullAction.FetchAll, GitPullAction.Merge, PullAction.FetchOnly, PullDialog.AllRemotes)]
    [TestCase(GitPullAction.FetchPruneAll, GitPullAction.Merge, PullAction.FetchOnly, PullDialog.AllRemotes)]
    public void The_dialog_starts_with_upstreams_choices_for_the_action(GitPullAction action, GitPullAction defaultAction,
        PullAction expected, string remote)
    {
        PullDialog dialog = new(_remotes, "main", action, defaultAction, defaultRemote: null, branchRemote: "origin");

        dialog.Remotes.Should().Equal(PullDialog.AllRemotes, "origin", "upstream");
        dialog.Action.Should().Be(expected);
        dialog.Remote.Should().Be(remote);
        dialog.Prune.Should().Be(action == GitPullAction.FetchPruneAll);
        dialog.LocalBranch.Should().Be(expected == PullAction.FetchOnly ? "" : "main");
    }

    [Test]
    public void The_remote_is_the_one_asked_for_else_the_branchs_else_the_first()
    {
        new PullDialog(_remotes, "main", GitPullAction.Merge, GitPullAction.Merge, "UPSTREAM", "origin").Remote.Should()
            .Be("upstream");
        new PullDialog(_remotes, "main", GitPullAction.Merge, GitPullAction.Merge, null, "upstream").Remote.Should()
            .Be("upstream");
        new PullDialog(_remotes, "main", GitPullAction.Merge, GitPullAction.Merge, null, "gone").Remote.Should()
            .Be("origin");
        new PullDialog([], "main", GitPullAction.Merge, GitPullAction.Merge, null, null).Remote.Should()
            .Be(PullDialog.AllRemotes);
    }

    [Test]
    public void The_options_follow_the_action_as_upstreams_radio_buttons()
    {
        PullDialog dialog = new(_remotes, "main", GitPullAction.Fetch, GitPullAction.Merge, null, "origin")
        {
            Tags = PullTags.All,
        };
        dialog.FetchOptionsEnabled.Should().BeTrue();
        dialog.ButtonText.Should().Be("&Fetch");
        dialog.Title("C:/repo").Should().Be("Fetch (C:/repo)");
        dialog.HelpImages.Should().Be(("HelpPullFetch", (string?)null));

        dialog.SetAction(PullAction.Merge);

        dialog.FetchOptionsEnabled.Should().BeFalse();
        dialog.Tags.Should().Be(PullTags.Reachable, "all tags are fetched only by a fetch");
        dialog.LocalBranch.Should().Be("main");
        dialog.ButtonText.Should().Be("&Pull");
        dialog.HelpImages.Should().Be(("HelpPullMerge", "HelpPullMergeFastForward"));

        dialog.Remote = PullDialog.AllRemotes;
        dialog.CanMergeOrRebase.Should().BeFalse();
        dialog.Source.Should().Be("--all");
        dialog.FromUrl = true;
        dialog.CanMergeOrRebase.Should().BeTrue();
    }

    [Test]
    public void ValidationError_should_name_what_is_missing()
    {
        PullDialog dialog = new(_remotes, "main", GitPullAction.Merge, GitPullAction.Merge, null, "origin");
        dialog.ValidationError().Should().BeNull();

        dialog.RemoteBranch = "*";
        dialog.ValidationError().Should().Be(PullDialog.FetchAllBranchesCanOnlyWithFetch);
        dialog.SetAction(PullAction.FetchOnly);
        dialog.ValidationError().Should().BeNull();

        dialog.FromUrl = true;
        dialog.ValidationError().Should().Be(PullDialog.SelectSourceDirectory);
    }

    [Test]
    public void CalculateBranches_should_pull_the_branchs_upstream_when_the_remote_is_the_branchs()
    {
        PullDialog dialog = new(_remotes, "main", GitPullAction.Merge, GitPullAction.Merge, null, "origin");

        PullBranches branches = dialog.CalculateBranches(_ => "origin");

        branches.Should().Be(new PullBranches(null, ""));
        dialog.Request(branches).Should().Be(new PullRequest("origin", "", PullAction.Merge, Prune: false,
            AutoStash: false, LocalBranch: "", FetchTags: null));
    }

    [Test]
    public void CalculateBranches_should_offer_the_local_branchs_name_on_another_remote()
    {
        PullDialog dialog = new(_remotes, "main", GitPullAction.Merge, GitPullAction.Merge, "upstream", "origin");

        PullBranches branches = dialog.CalculateBranches(_ => "origin");

        branches.QuestionButton.Should().Be("Pull from upstream/main");
        branches.Question.Should().StartWith("You asked to pull from the remote 'upstream',");
        branches.Confirmed().Should().Be(new PullBranches("main", "main"));
    }

    [Test]
    public void CalculateBranches_should_fetch_another_branch_only_with_its_remote_branch()
    {
        PullDialog dialog = new(_remotes, "main", GitPullAction.Fetch, GitPullAction.Merge, null, "origin")
        {
            LocalBranch = "topic",
        };

        dialog.CalculateBranches(_ => null).QuestionButton.Should().Be("Fetch from origin/topic");

        dialog.LocalBranch = "main";
        dialog.CalculateBranches(_ => "origin").Should().Be(new PullBranches(null, ""));

        dialog.LocalBranch = "topic";
        dialog.RemoteBranch = "feature";
        dialog.CalculateBranches(_ => null).Should().Be(new PullBranches("topic", "feature"));
    }

    [Test]
    public void CalculateBranches_should_name_no_branch_for_all_remotes_or_a_detached_head()
    {
        new PullDialog(_remotes, "main", GitPullAction.FetchAll, GitPullAction.Merge, null, "origin")
            .CalculateBranches(_ => null).Should().Be(new PullBranches(null, null));
        PullDialog detached = new(_remotes, "", GitPullAction.Merge, GitPullAction.Merge, null, null);
        detached.IsDetached.Should().BeTrue();
        detached.CalculateBranches(_ => null).Should().Be(new PullBranches(null, ""));
    }

    [Test]
    public void Request_should_carry_upstreams_fetch_options()
    {
        PullDialog dialog = new(_remotes, "main", GitPullAction.Fetch, GitPullAction.Merge, null, "origin")
        {
            Tags = PullTags.All, Unshallow = true, Prune = true, PruneTags = true, AutoStash = true,
        };

        dialog.Request(new PullBranches("topic", "feature")).Should().Be(new PullRequest("origin", "feature",
            PullAction.FetchOnly, Prune: true, AutoStash: false, LocalBranch: "topic", FetchTags: true, Unshallow: true,
            PruneTags: true));
        dialog.SetAction(PullAction.Rebase);
        dialog.Tags = PullTags.None;
        dialog.Request(new PullBranches(null, "")).Should().Be(new PullRequest("origin", "", PullAction.Rebase,
            Prune: false, AutoStash: true, LocalBranch: "", FetchTags: false, Unshallow: true, PruneTags: false));
        dialog.PulledAction.Should().Be(GitPullAction.Rebase);
    }

    private static string TestFolder([CallerFilePath] string testFile = "") => Path.GetDirectoryName(testFile)!;
}
