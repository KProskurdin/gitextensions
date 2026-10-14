using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitCommands;
using GitCommands.Git;
using GitCommands.Remotes;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class PushDialogTests
{
    private static readonly ObjectId _one = ObjectId.Parse("1111111111111111111111111111111111111111");
    private static readonly ObjectId _two = ObjectId.Parse("2222222222222222222222222222222222222222");

    [Test]
    public void The_texts_are_upstreams()
    {
        string root = Path.GetFullPath(Path.Combine(TestFolder(), "..", "..", ".."));
        string form = File.ReadAllText(Path.Combine(root, "src", "app", "GitUI", "CommandsDialogs", "FormPush.cs"))
            .Replace("{Environment.NewLine}", "\n");
        string designer = File.ReadAllText(Path.Combine(root, "src", "app", "GitUI", "CommandsDialogs",
            "FormPush.Designer.cs"));
        foreach (FieldInfo field in typeof(PushDialog).GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(field =>
                         field.FieldType == typeof(string) && field.Name is not nameof(PushDialog.HeadText)))
        {
            string text = ((string)field.GetValue(null)!).Replace(Environment.NewLine, "\n").Replace("\"", "\\\"");
            form.Should().Contain(text, field.Name);
        }

        foreach (string choice in PushDialog.RecursiveSubmoduleChoices)
        {
            designer.Should().Contain($"\"{choice}\"");
        }
    }

    [Test]
    public void The_remote_is_the_branchs_else_origin_else_the_first()
    {
        Repository("upstream").Remote.Should().Be("upstream");
        Repository(null).Remote.Should().Be("origin");
        new PushDialog(Data(null) with { Remotes = [Remote("fork")] }).Remote.Should().Be("fork");
    }

    [Test]
    public void The_remote_branch_is_the_push_target_else_the_tracked_branch_else_the_branchs_name()
    {
        PushDialog dialog = Repository("origin");
        dialog.Branch.Should().Be("main");
        dialog.RemoteBranch.Should().Be("trunk", "main tracks origin/trunk");
        dialog.Destination.Should().Be("git@example.com:push.git", "the push URL comes before the URL");

        dialog.Branch = "topic";
        dialog.RemoteBranch.Should().Be("topic");

        PushDialog pushTarget = new(Data("origin") with { DefaultPushRemote = (_, branch) => $"review/{branch}" });
        pushTarget.RemoteBranch.Should().Be("review/main");

        dialog.Branch = PushDialog.AllRefs;
        dialog.RemoteBranch.Should().BeEmpty();
        dialog.Request().Kind.Should().Be(PushKind.AllBranches);

        new PushDialog(Data("origin") with { CurrentBranch = "" }).Branch.Should().Be(PushDialog.HeadText);
    }

    [Test]
    public void The_lists_are_upstreams()
    {
        PushDialog dialog = Repository("origin");

        dialog.Branches.Should().Equal(PushDialog.AllRefs, PushDialog.HeadText, "main", "topic");
        dialog.RemoteBranches.Should().Equal("main", "trunk", "old");
        dialog.Tags.Should().Equal(PushDialog.AllRefs, "v1");
    }

    [Test]
    public void Force_and_force_with_lease_exclude_each_other_and_a_tag_push_forces()
    {
        PushDialog dialog = Repository("origin");
        dialog.Force = true;
        dialog.ForceWithLease = true;
        dialog.Force.Should().BeFalse();
        dialog.Request().Force.Should().Be(ForcePushOptions.ForceWithLease);

        dialog.Tab = PushTab.Tags;
        dialog.Tag = "v1";
        dialog.Request().Should()
            .Be(new PushRequest("origin", "", "", ForcePushOptions.Force, false, PushKind.Tag, "v1"));
        dialog.Tag = PushDialog.AllRefs;
        dialog.Request().Kind.Should().Be(PushKind.AllTags);
    }

    [Test]
    public void ValidationError_should_name_what_is_missing()
    {
        PushDialog dialog = Repository("origin");
        dialog.ValidationError().Should().BeNull();

        dialog.RemoteBranch = "";
        dialog.ValidationError().Should().Be(PushDialog.NoCurrentBranch);
        dialog.Tab = PushTab.Tags;
        dialog.ValidationError().Should().Be(PushDialog.SelectTag);
        dialog.ToUrl = true;
        dialog.Destination = "not a url";
        dialog.ValidationError().Should().Be(PushDialog.SelectDestinationDirectory);
        new PushDialog(Data(null) with { Remotes = [] }).ValidationError().Should().Be(PushDialog.ConfigureRemote);
    }

    [Test]
    public void A_branch_the_remote_does_not_have_is_new_and_one_without_tracking_is_offered_a_reference()
    {
        PushDialog dialog = Repository("origin");
        dialog.IsNewBranchForRemote.Should().BeFalse("origin has trunk");
        dialog.TrackingQuestion.Should().BeNull("main tracks a branch");

        dialog.Branch = "topic";
        dialog.IsNewBranchForRemote.Should().BeTrue();
        dialog.TrackingQuestion.Should().Be(
            "The branch topic does not have a tracking reference. Do you want to add a tracking reference to topic?");
        dialog.Request(track: true).Should().Be(new PushRequest("origin", "topic", "topic", ForcePushOptions.DoNotForce,
            true, RecursiveSubmodules: 0));

        new PushDialog(Data("origin") with { AutoSetupMerge = "false" }, "topic").TrackingQuestion.Should().BeNull();
    }

    [Test]
    public void The_multiple_branches_rows_compare_with_the_remote_and_their_choices_exclude_each_other()
    {
        PushRepository data = Data("origin");
        PushDialog dialog = new(data with
        {
            RemoteBranches = [.. data.RemoteBranches, new FakeRef("topic", "origin", _two)],
            AheadBehind = new Dictionary<string, AheadBehindData>
            {
                ["main"] = new("main", "refs/remotes/origin/trunk", "1", ""),
            },
        });
        dialog.Tab = PushTab.MultipleBranches;

        dialog.LoadMultipleBranches();

        dialog.MultipleBranches.Select(row => (row.Local, row.Remote, row.AheadBehind)).Should().Equal(
            ("main", "trunk", "1↑"), ("topic", "", "<>"), ("", "trunk", ""), ("", "old", ""));
        PushBranchRow topic = dialog.MultipleBranches[1];
        topic.Push = true;
        topic.Force = true;
        topic.Push.Should().BeFalse();
        dialog.MultipleBranches[3].Delete = true;
        dialog.SelectTracked();
        dialog.MultipleBranches[0].Push.Should().BeTrue();
        dialog.MultipleBranches[1].Force.Should().BeTrue("select tracked sets only the push choices");

        PushRequest request = dialog.Request();

        request.Kind.Should().Be(PushKind.Multiple);
        request.Actions!.Select(action => action.ToString()).Should().Equal(
            "refs/heads/main:refs/heads/trunk", "+refs/heads/topic:refs/heads/topic", ":refs/heads/old");
    }

    private static PushDialog Repository(string? branchRemote) => new(Data(branchRemote));

    private static PushRepository Data(string? branchRemote)
        => new("main",
            [Remote("origin", pushUrl: "git@example.com:push.git"), Remote("upstream")],
            [
                new FakeRef("main", "", _one) { TrackingRemote = "origin", MergeWith = "trunk" },
                new FakeRef("topic", "", _one)
            ],
            [new FakeRef("trunk", "origin", _one), new FakeRef("old", "origin", _two)],
            ["v1"],
            branchRemote,
            null,
            IsBare: false,
            AutoSetupMerge: null,
            (_, _) => null);

    private static ConfigFileRemote Remote(string name, string? pushUrl = null)
        => new() { Name = name, Url = $"https://example.com/{name}.git", PushUrl = pushUrl };

    private static string TestFolder([CallerFilePath] string testFile = "") => Path.GetDirectoryName(testFile)!;

    // A local branch (no remote) or a remote branch, without a module behind it.
    private sealed class FakeRef(string name, string remote, ObjectId id) : IGitRef
    {
        public string Name => name;

        public ObjectId ObjectId => id;

        public string? Guid => id.ToString();

        public string CompleteName => remote.Length == 0 ? $"refs/heads/{name}" : $"refs/remotes/{remote}/{name}";

        public string LocalName => name;

        public string MergeWith { get; set; } = "";

        public string Remote => remote;

        public string TrackingRemote { get; set; } = "";

        public bool IsHead => remote.Length == 0;

        public bool IsRemote => remote.Length > 0;

        public bool IsTag => false;

        public bool IsDereference => false;

        public bool IsStash => false;

        public bool IsSelected { get; set; }

        public bool IsSelectedHeadMergeSource { get; set; }

        public bool IsBisect => false;

        public bool IsBisectGood => false;

        public bool IsBisectBad => false;

        public IGitModule Module => throw new NotSupportedException();

        public bool IsTrackingRemote(IGitRef? other) => false;
    }
}