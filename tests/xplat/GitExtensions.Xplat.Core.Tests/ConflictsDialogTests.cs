using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class ConflictsDialogTests
{
    private static readonly ObjectId _id = ObjectId.Parse("0123456789abcdef0123456789abcdef01234567");

    [Test]
    public void The_texts_are_upstreams()
    {
        string source = UpstreamSource("src/app/GitUI/CommandsDialogs/FormResolveConflicts.cs").Replace(
            "\" + Environment.NewLine + \"", "\n").Replace("\" + Environment.NewLine + Environment.NewLine + \"", "\n\n")
            .Replace("\" +\n            \"", "").Replace(" + Environment.NewLine +\n            \"", "\n")
            .Replace("\" + Environment.NewLine +\n            Environment.NewLine + \"", "\n\n");
        foreach (string text in new[]
                 {
                     ConflictsDialog.ChooseLocalRebaseText, ConflictsDialog.ChooseRemoteMergeText,
                     ConflictsDialog.TakeOnlyLocalMergeTooltip, ConflictsDialog.SolveMergeConflictApplyToAll,
                     ConflictsDialog.FileChangeLocallyAndRemotely, ConflictsDialog.FileModifiedLocallyAndDeletedRemotely,
                     ConflictsDialog.ChooseBaseTooltip, ConflictsDialog.KeepBaseButtonText, ConflictsDialog.StageFilename,
                 })
        {
            source.Should().Contain($"\"{text}\"");
        }

        string designer = UpstreamSource("src/app/GitUI/CommandsDialogs/FormResolveConflicts.Designer.cs");
        designer.Should().Contain($"\"{ConflictsDialog.LocalLabel}\"").And.Contain($"\"{ConflictsDialog.RemoteLabel}\"")
            .And.Contain($"\"{ConflictsDialog.SelectFile}\"");
    }

    [Test]
    public void Ours_and_theirs_swap_during_a_rebase()
    {
        ConflictsDialog merge = new(inRebase: false);
        ConflictsDialog rebase = new(inRebase: true);

        (merge.LocalSide, merge.RemoteSide, rebase.LocalSide, rebase.RemoteSide).Should()
            .Be(("ours", "theirs", "theirs", "ours"));
        merge.LocalLabelText("Local/current").Should().Be("Local/current\u00A0(ours)");
        rebase.ChooseRemoteText.Should().Be(ConflictsDialog.ChooseRemoteRebaseText);
    }

    [Test]
    public void The_description_and_names_say_which_sides_have_the_file()
    {
        ConflictsDialog dialog = new(inRebase: false);
        ConflictData changed = Conflict(baseName: "a.txt", local: "a.txt", remote: "a.txt");
        ConflictData deletedRemotely = Conflict(baseName: "a.txt", local: "a.txt", remote: null);
        ConflictData added = Conflict(baseName: null, local: "a.txt", remote: "a.txt");

        dialog.Description(changed).Should().Be(string.Format(ConflictsDialog.FileChangeLocallyAndRemotely, "ours", "theirs"));
        dialog.Description(added).Should().Be(string.Format(ConflictsDialog.FileCreatedLocallyAndRemotely, "ours", "theirs"));
        dialog.Description(Conflict(null, null, "a.txt")).Should().BeNull();
        ConflictsDialog.Names(deletedRemotely, isSubmodule: false).Should().Be(("a.txt", "a.txt", "deleted"));
        ConflictsDialog.Names(added, isSubmodule: true).Should()
            .Be(("no base@deleted", "a.txt@01234567", "a.txt@01234567"));
    }

    [Test]
    public void The_selected_files_are_grouped_as_upstream_resolves_them()
    {
        ConflictData deletedLocally = Conflict("a", null, "a");
        ConflictData deletedRemotely = Conflict("b", "b", null);
        ConflictData first = Conflict("c", "c", "c");
        ConflictData second = Conflict("d", "d", "d");

        (IReadOnlyList<ConflictData> locally, IReadOnlyList<ConflictData> remotely, IReadOnlyList<ConflictData> rest) =
            ConflictsDialog.Classify([first, deletedLocally, second, deletedRemotely]);

        locally.Should().Equal(deletedLocally);
        remotely.Should().Equal(deletedRemotely);
        rest.Should().Equal(second, first);
    }

    [Test]
    public void The_question_for_a_deleted_side_counts_the_files_left()
    {
        ConflictsDialog dialog = new(inRebase: false);
        ConflictData item = Conflict("a.txt", "a.txt", null);

        dialog.DeletedSideText(item, deletedLocally: false, unsolved: 1, selected: 1, othersSelected: false,
            applyToAll: false).Should().StartWith("File 'a.txt' does not have a remote revision.");
        dialog.DeletedSideText(item, false, unsolved: 3, selected: 5, othersSelected: true, applyToAll: false).Should()
            .StartWith("'a.txt' and 2 other out of 5 selected files do not have a remote revision.");
        dialog.DeletedSideText(item, false, unsolved: 3, selected: 3, othersSelected: false, applyToAll: false).Should()
            .StartWith("'a.txt' and 2 other selected file(s) do not have a remote revision.");
        dialog.DeletedSideText(item, false, 3, 3, false, applyToAll: true).Should().BeEmpty();
        dialog.DeletedSideButtons(deletedLocally: false).Should()
            .Be(("Keep modified (ours)", "Delete file (theirs)", "Keep base file"));
        dialog.DeletedSideButtons(deletedLocally: true).Should()
            .Be(("Delete file (ours)", "Keep modified (theirs)", "Keep base file (ours)"));
    }

    [Test]
    public void The_merge_tool_is_read_as_upstream_reads_it()
    {
        Dictionary<string, string> settings = new()
        {
            ["merge.guitool"] = "gui",
            ["merge.tool"] = "kdiff3",
            ["mergetool.gui.cmd"] = "\"C:/Tools/Gui.exe\" \"$LOCAL\" \"$REMOTE\"",
        };
        string Setting(string key) => settings.GetValueOrDefault(key, "");
        bool Found(string name, out string? fullPath)
        {
            fullPath = "/found/" + name;
            return name.Length > 0;
        }

        ConflictsDialog.ReadMergeTool(Setting, supportsGuiTool: false, isWindows: false, Found).Should()
            .Be(new MergeTool("kdiff3", "\"$BASE\" \"$LOCAL\" \"$REMOTE\" -o \"$MERGED\"", "/found/kdiff3", null));
        ConflictsDialog.ReadMergeTool(Setting, supportsGuiTool: true, isWindows: true, Found).Should()
            .Be(new MergeTool("gui", " \"$LOCAL\" \"$REMOTE\"", "/found/C:/Tools/Gui.exe", null));
        ConflictsDialog.ReadMergeTool(_ => "", true, false, Found).Error.Should().Be(ConflictsDialog.NoMergeTool);
        ConflictsDialog.ReadMergeTool(key => key == "merge.tool" ? "missing" : "", true, false, Found).Error.Should()
            .Be(ConflictsDialog.NoMergeToolConfigured);
    }

    [Test]
    public void The_tools_command_and_its_result_are_upstreams()
    {
        ConflictsDialog.TwoWayMergeArguments("KDiff3", "\"$BASE\" \"$LOCAL\" \"$REMOTE\" -o \"$MERGED\"").Should()
            .Be(" \"$LOCAL\" \"$REMOTE\" -o \"$MERGED\"");
        ConflictsDialog.TwoWayMergeArguments("tortoisemerge", "/base:\"$BASE\" /mine:\"$LOCAL\"").Should()
            .Be(" /base:\"$LOCAL\"");
        ConflictsDialog.MergeToolArguments("\"$BASE\" \"$LOCAL\" \"$REMOTE\" -o \"$MERGED\"", "b", "l", "r", "a.txt")
            .Should().Be("\"b\" \"l\" \"r\" -o \"a.txt\"");
        ConflictsDialog.Outcome(0, fileChanged: true).Should().Be(MergeToolOutcome.Stage);
        ConflictsDialog.Outcome(0, fileChanged: false).Should().Be(MergeToolOutcome.Ask);
        ConflictsDialog.Outcome(1, fileChanged: true).Should().Be(MergeToolOutcome.Ask);
        ConflictsDialog.Outcome(1, fileChanged: false).Should().Be(MergeToolOutcome.None);
    }

    private static ConflictData Conflict(string? baseName, string? local, string? remote)
        => new(Side(baseName), Side(local), Side(remote));

    private static ConflictedFileData Side(string? name) => name is null ? default : new ConflictedFileData(_id, name);

    private static string UpstreamSource(string relativePath, [System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", relativePath)));
}
