using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using GitUIPluginInterfaces;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class StashDialogTests
{
    private static readonly GitStash[] _stashes =
        [new(0, "On main: newest"), new(1, "On main: middle"), new(2, "On main: oldest")];

    [Test]
    public void The_texts_are_upstreams()
    {
        string formStash = UpstreamSource("src/app/GitUI/CommandsDialogs/FormStash.cs");
        formStash.Should().Contain($"\"{StashDialog.CurrentWorkingDirChanges}\"").And
            .Contain($"\"{StashDialog.NoStashes}\"");
        string translated = UpstreamSource("src/app/GitUI/TranslatedStrings.cs");
        translated.Should().Contain($"\"{StashDialog.DropConfirmTitle}\"").And
            .Contain($"\"{StashDialog.DontShowAgain}\"");
        string resources = UpstreamSource("src/app/ResourceManager/TranslatedStrings.cs");
        resources.Should().Contain($"\"{StashDialog.IndexGroup}\"").And.Contain($"\"{StashDialog.WorkspaceGroup}\"");
    }

    [Test]
    public void Load_should_put_the_working_directory_first_and_show_the_newest_stash_on_the_first_load_only()
    {
        StashDialog dialog = new(manageStashes: true);

        dialog.Load(_stashes);

        dialog.Items.Select(stash => stash.Summary).Should().Equal(
            StashDialog.CurrentWorkingDirChanges, "@{0}: On main: newest", "@{1}: On main: middle",
            "@{2}: On main: oldest");
        dialog.SelectedIndex.Should().Be(1);
        dialog.CanApplyOrDrop.Should().BeTrue();
        dialog.IsMessageReadOnly.Should().BeTrue();
        dialog.Message.Should().Be("On main: newest");

        dialog.Load(_stashes);

        dialog.IsWorkingDirectorySelected.Should()
            .BeTrue("as upstream, a later load (after a stash) shows the working directory");
    }

    [Test]
    public void Load_should_show_the_working_directory_when_creating_a_stash_or_without_stashes()
    {
        StashDialog creating = new(manageStashes: false);
        creating.Load(_stashes);

        creating.IsWorkingDirectorySelected.Should().BeTrue();
        creating.CanApplyOrDrop.Should().BeFalse();
        creating.IsMessageReadOnly.Should().BeFalse();
        creating.Message.Should().BeEmpty();

        StashDialog empty = new();
        empty.Load([]);

        empty.IsWorkingDirectorySelected.Should().BeTrue();
        empty.Message.Should().Be(StashDialog.NoStashes);
    }

    [Test]
    public void An_initial_stash_is_shown_first()
    {
        StashDialog dialog = new(manageStashes: true, initialStash: "stash@{2}");

        dialog.Load(_stashes);

        dialog.Selected!.Name.Should().Be("stash@{2}");
    }

    [Test]
    public void After_a_drop_the_stash_that_takes_its_place_is_shown_or_the_new_last_one()
    {
        StashDialog dialog = new();
        dialog.Load(_stashes);
        dialog.Select(2);

        dialog.Dropping();
        dialog.Load([_stashes[0], new GitStash(1, "On main: oldest")]);

        dialog.Selected!.Message.Should().Be("On main: oldest");

        dialog.Dropping();
        dialog.Load([_stashes[0]]);

        dialog.Selected!.Message.Should().Be("On main: newest");
    }

    [Test]
    public void SelectNeighbour_should_move_towards_the_newer_stashes_for_next_and_stop_at_the_ends()
    {
        StashDialog dialog = new();
        dialog.Load(_stashes);

        dialog.SelectNeighbour(next: true).Should().BeTrue();
        dialog.IsWorkingDirectorySelected.Should().BeTrue();
        dialog.SelectNeighbour(next: true).Should().BeFalse();

        dialog.Select(3);
        dialog.SelectNeighbour(next: false).Should().BeFalse();
        dialog.SelectedIndex.Should().Be(3);
    }

    [Test]
    public void Selected_changes_can_be_stashed_only_from_the_working_directory()
    {
        StashDialog dialog = new(manageStashes: false);
        dialog.Load(_stashes);

        dialog.CanStashSelected(selectedFiles: 0).Should().BeFalse();
        dialog.CanStashSelected(selectedFiles: 2).Should().BeTrue();

        dialog.Select(1);
        dialog.CanStashSelected(selectedFiles: 2).Should().BeFalse();
    }

    [TestCase(null, "")]
    [TestCase("   ", "")]
    [TestCase("  work in progress \n", " work in progress")]
    public void MessageArgument_should_be_upstreams(string? typed, string expected)
        => StashDialog.MessageArgument(typed).Should().Be(expected);

    private static string UpstreamSource(string relativePath,
        [System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => File.ReadAllText(
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", relativePath)));
}
