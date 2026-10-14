using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class PushRejectionTests
{
    private static readonly string[] _rejectedMain =
    [
        "To example.com:repo.git",
        " ! [rejected]        main -> main (fetch first)",
        "error: failed to push some refs to 'example.com:repo.git'",
    ];

    [Test]
    public void The_texts_are_upstreams()
    {
        string root = Path.GetFullPath(Path.Combine(TestFolder(), "..", "..", ".."));
        // Upstream splits the long texts over two literals.
        string form = Regex.Replace(
            File.ReadAllText(Path.Combine(root, "src", "app", "GitUI", "CommandsDialogs", "FormPush.cs")),
            "\"\\s*\\+\\s*\"", "");
        foreach (FieldInfo field in typeof(PushRejection).GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(field => field.FieldType == typeof(string)))
        {
            string text = ((string)field.GetValue(null)!).Replace("\"", "\\\"");
            form.Should().Contain(text, field.Name);
        }
    }

    [Test]
    public void Find_should_tell_a_rejected_current_branch_from_another_rejected_ref()
    {
        PushRejection.Find(_rejectedMain, "main", "main", toUrl: false, isBare: false).Should().BeTrue();
        PushRejection.Find([" ! [rejected]        v1 -> v1 (already exists)"], "main", "main", false, false)
            .Should().BeFalse();
        PushRejection.Find(["error: src refspec main does not match any"], "main", "main", false, false).Should()
            .BeNull();
    }

    [Test]
    public void Find_should_leave_pushes_upstream_does_not_handle()
    {
        PushRejection.Find(_rejectedMain, "topic", "main", false, false).Should().BeNull("another branch was pushed");
        PushRejection.Find(_rejectedMain, "main", "main", toUrl: true, false).Should().BeNull();
        PushRejection.Find(_rejectedMain, "main", "main", false, isBare: true).Should().BeNull();
        PushRejection.Find(_rejectedMain, "main", "main", false, false, PushKind.Multiple).Should().BeNull();
        PushRejection.Find(_rejectedMain, "", "", false, false).Should().BeNull("a detached HEAD has no branch");
    }

    [Test]
    public void PullFor_should_follow_the_default_action_and_refuse_a_fetch()
    {
        PushRejection.PullFor(GitPullAction.Default, GitPullAction.Rebase, out string? error).Should()
            .Be(PullAction.Rebase);
        error.Should().BeNull();
        PushRejection.PullFor(GitPullAction.Merge, GitPullAction.Rebase, out _).Should().Be(PullAction.Merge);
        PushRejection.PullFor(GitPullAction.None, GitPullAction.Rebase, out error).Should().BeNull();
        error.Should().BeNull();
        PushRejection.PullFor(GitPullAction.Default, GitPullAction.Fetch, out error).Should().BeNull();
        error.Should().Be(PushRejection.PullActionNotSupported);
    }

    [Test]
    public void WithForceWithLease_should_keep_a_push_that_already_forces()
    {
        PushRequest plain = new("origin", "main", "", ForcePushOptions.DoNotForce, Track: false);

        PushRejection.WithForceWithLease(plain).Force.Should().Be(ForcePushOptions.ForceWithLease);
        PushRejection.WithForceWithLease(plain with { Force = ForcePushOptions.Force }).Force.Should()
            .Be(ForcePushOptions.Force);
    }

    [Test]
    public void The_merge_commit_check_applies_to_the_current_branch_pushed_to_its_remote_when_the_default_rebases()
    {
        PushRequest request = new("origin", "main", "main", ForcePushOptions.DoNotForce, Track: false);

        PushRejection.ChecksRebasedMergeCommits(GitPullAction.Rebase, request, false, "main", "main", "origin")
            .Should().BeTrue();
        PushRejection.ChecksRebasedMergeCommits(GitPullAction.Merge, request, false, "main", "main", "origin")
            .Should().BeFalse();
        PushRejection.ChecksRebasedMergeCommits(GitPullAction.Rebase, request, false, "main", "main", "upstream")
            .Should().BeFalse();
        PushRejection.ChecksRebasedMergeCommits(GitPullAction.Rebase, request with { Kind = PushKind.Tag }, false,
            "main", "main", "origin").Should().BeFalse();
    }

    [Test]
    public void DefaultButtonAction_should_name_the_action_as_upstream()
    {
        PushRejection.DefaultButtonAction(GitPullAction.FetchPruneAll).Should().Be(PushRejection.ActionFetch);
        PushRejection.DefaultButtonAction(GitPullAction.Rebase).Should().Be(PushRejection.ActionRebase);
        PushRejection.DefaultButtonAction(GitPullAction.None).Should().Be(PushRejection.ActionNone);
    }

    private static string TestFolder([CallerFilePath] string testFile = "") => Path.GetDirectoryName(testFile)!;
}