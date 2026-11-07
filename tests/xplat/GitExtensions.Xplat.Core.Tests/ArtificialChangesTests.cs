using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class ArtificialChangesTests
{
    [Test]
    public void Of_should_group_the_changes_as_upstreams_ArtificialCommitChangeCount_with_its_summary()
    {
        ArtificialChanges changes = ArtificialChanges.Of(
        [
            new FileChange("a.txt", ChangeKind.Modified, Staged: false),
            new FileChange("moved.txt", ChangeKind.Renamed, Staged: false),
            new FileChange("new.txt", ChangeKind.Untracked, Staged: false),
            new FileChange("added.txt", ChangeKind.Added, Staged: false),
            new FileChange("gone.txt", ChangeKind.Deleted, Staged: false),
            new FileChange("lib", ChangeKind.Modified, Staged: false)
            {
                IsSubmodule = true, IsSubmoduleChanged = true, IsSubmoduleDirty = true
            },
            new FileChange("dirty", ChangeKind.Modified, Staged: false) { IsSubmodule = true, IsSubmoduleDirty = true },
        ]);

        (changes with { Summary = null }).Should()
            .Be(new ArtificialChanges(Changed: 2, New: 2, Deleted: 1, SubmodulesChanged: 1, SubmodulesDirty: 2));
        changes.Summary!.ReplaceLineEndings("\n").Should().Be(
            "2 changed files\n- a.txt\n- moved.txt\n\n"
            + "1 deleted file\n- gone.txt\n\n"
            + "2 new files\n- new.txt\n- added.txt\n\n"
            + "1 changed submodule\n- lib\n\n"
            + "2 dirty submodules\n- lib\n- dirty\n");
        changes.Parts.Select(part => (part.Image, part.Count)).Should().Equal(
            ("FileStatusModified", 2),
            ("FileStatusAdded", 2),
            ("FileStatusRemoved", 1),
            ("SubmoduleRevisionDown", 1),
            ("SubmoduleDirty", 2));
    }

    [Test]
    public void Of_should_list_five_files_of_a_group_and_count_the_others()
    {
        ArtificialChanges changes = ArtificialChanges.Of(
            [.. Enumerable.Range(1, 7).Select(i => new FileChange($"f{i}.txt", ChangeKind.Modified, Staged: true))]);

        changes.Summary!.ReplaceLineEndings("\n").Should()
            .Be("7 changed files\n- f1.txt\n- f2.txt\n- f3.txt\n- f4.txt\n- f5.txt\n- (2 more files)\n");
    }

    [Test]
    public void A_clean_row_shows_upstreams_clean_image_and_no_tooltip()
    {
        ArtificialChanges changes = ArtificialChanges.Of([]);

        changes.HasChanges.Should().BeFalse();
        changes.Summary.Should().BeNull();
        changes.Parts.Should().Equal(new ArtificialChangePart("RepoStateClean", null));
    }

    [Test]
    public void Before_the_status_is_read_a_row_shows_upstreams_unknown_image_and_counts_as_without_changes()
    {
        ArtificialChanges.Unknown.IsKnown.Should().BeFalse();
        ArtificialChanges.Unknown.HasChanges.Should().BeFalse();
        ArtificialChanges.Unknown.Summary.Should().BeNull();
        ArtificialChanges.Unknown.Parts.Should().Equal(new ArtificialChangePart("RepoStateUnknown", null));
        ArtificialChanges.Unknown.Should().NotBe(ArtificialChanges.Of([]));
    }

    [TestCase(false, false, RepoState.RepoStateDirty)]
    [TestCase(false, true, RepoState.RepoStateDirtySubmodules)]
    [TestCase(true, true, RepoState.RepoStateMixed)]
    public void RepoStates_should_be_upstreams_RepoStateVisualiser_state(bool staged, bool submodulesOnly,
        RepoState expected)
    {
        List<FileChange> changes =
        [
            new FileChange("lib", ChangeKind.Modified, Staged: false) { IsSubmodule = true, IsSubmoduleDirty = true },
        ];
        if (!submodulesOnly)
        {
            changes.Add(new FileChange("a.txt", ChangeKind.Modified, Staged: false));
        }

        if (staged)
        {
            changes.Add(new FileChange("b.txt", ChangeKind.Modified, Staged: true));
        }

        RepoStates.Of(changes).Should().Be(expected);
    }
}
