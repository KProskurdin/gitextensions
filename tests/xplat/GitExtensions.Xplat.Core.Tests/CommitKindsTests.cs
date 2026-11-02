using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CommitKindsTests
{
    [Test]
    public void Message_should_put_upstreams_prefix_before_the_subject_once()
    {
        CommitKinds.Message(CommitKind.Fixup, "Add parser", "Add parser\n\nbody").Should().Be("fixup! Add parser");
        CommitKinds.Message(CommitKind.Squash, "Add parser", "Add parser").Should().Be("squash! Add parser");
        CommitKinds.Message(CommitKind.Fixup, "fixup! Add parser", "fixup! Add parser").Should()
            .Be("fixup! Add parser");
        CommitKinds.Message(CommitKind.Normal, "Add parser", "Add parser").Should().BeNull();
    }

    [Test]
    public void Message_should_follow_an_amend_prefix_with_the_whole_message()
        => CommitKinds.Message(CommitKind.Amend, "Add parser", "Add parser\n\nbody")
            .Should().Be("amend! Add parser\n\nAdd parser\n\nbody");

    [Test]
    public void Only_a_fixup_or_squash_message_is_read_only_and_not_kept_as_the_draft()
    {
        CommitKinds.MessageCanBeChanged(CommitKind.Normal).Should().BeTrue();
        CommitKinds.MessageCanBeChanged(CommitKind.Amend).Should().BeTrue();
        CommitKinds.MessageCanBeChanged(CommitKind.Fixup).Should().BeFalse();
        CommitKinds.KeepsDraft(CommitKind.Squash).Should().BeFalse();
    }

    [Test]
    public void RewriteCommit_should_rebase_interactively_onto_the_first_parent_with_autostash()
    {
        ObjectId parent = ObjectId.Parse("0123456789012345678901234567890123456789");

        RewriteCommit.Arguments(parent, supportRebaseMerges: true).ToString()
            .Should().Be($"-c rebase.autosquash=false rebase -i --no-autosquash --autostash \"{parent}\"");
    }

    [Test]
    public void SequenceEditor_should_turn_the_first_pick_into_the_command()
    {
        RewriteCommit.SequenceEditor(RewriteCommit.Edit, onMac: false).Should().Be("sed -i -re '0,/pick/s//e/'");
        RewriteCommit.SequenceEditor(RewriteCommit.Reword, onMac: true)
            .Should().Be("perl -i -pe '$d = s/^pick/r/ unless $d'");
    }
}
