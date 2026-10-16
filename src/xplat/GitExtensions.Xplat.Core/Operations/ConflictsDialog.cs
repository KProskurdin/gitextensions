using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  What upstream's resolve conflicts dialog does after the merge tool closed (<c>ResolveFilesConflictAsync</c>).
/// </summary>
public enum MergeToolOutcome
{
    /// <summary>
    ///  Leave the file unresolved.
    /// </summary>
    None,

    /// <summary>
    ///  The tool saved the file and succeeded: stage it.
    /// </summary>
    Stage,

    /// <summary>
    ///  The tool and the file disagree: ask whether the conflict is solved, and stage it if so.
    /// </summary>
    Ask,
}

/// <summary>
///  Upstream's choices for a file without a base, local or remote version (<c>ConflictResolutionPreference</c>): which
///  command link of the "Solve merge conflict" dialog was chosen.
/// </summary>
public enum ConflictChoice
{
    None,
    KeepLocal,
    KeepRemote,
    KeepBase,
}

/// <summary>
///  The merge tool upstream's <c>InitMergetool</c> reads: the tool, the command line its <c>cmd</c> setting gives (with
///  <c>$BASE</c>, <c>$LOCAL</c>, <c>$REMOTE</c> and <c>$MERGED</c>) and the program; or the error it shows.
/// </summary>
public sealed record MergeTool(string? Name, string? Command, string? Path, string? Error)
{
    /// <summary>
    ///  Upstream's <c>mergeToolExtrasConfigured</c>: the app can run the tool itself, not only through git mergetool.
    /// </summary>
    public bool HasExtras => !string.IsNullOrWhiteSpace(Path) || !string.IsNullOrWhiteSpace(Command);
}

/// <summary>
///  Upstream's <c>FormResolveConflicts</c> without its window: the texts, which side is ours during a merge or a rebase, the
///  description of a conflicted file, the questions for a file deleted on one side, the merge tool and its command line, and
///  what to do after the tool ran.
/// </summary>
public sealed class ConflictsDialog
{
    // Upstream's texts.
    public const string Ours = "ours";
    public const string Theirs = "theirs";
    public const string NoBase = "no base";
    public const string Deleted = "deleted";
    public const string SelectFile = "Select file";
    public const string LocalLabel = "Local/current";
    public const string RemoteLabel = "Remote/incoming";
    public const string OpenIn = "Open in";
    public const string ChooseLocalButtonText = "Choose local";
    public const string ChooseRemoteButtonText = "Choose remote";
    public const string DeleteFileButtonText = "Delete file";
    public const string KeepModifiedButtonText = "Keep modified";
    public const string KeepBaseButtonText = "Keep base file";
    public const string SolveMergeConflictApplyToAll = "Apply to '{0}' and {1} other file(s)";
    public const string SolveMergeConflictCaption = "Solve merge conflict";
    public const string ChooseLocalRebaseText = "Choose local/current (theirs)";
    public const string TakeOnlyLocalRebaseTooltip = "Take only the changes from the branch you are rebasing onto";
    public const string LocalRebaseTooltip = "Changes from the branch you are rebasing onto";
    public const string ChooseRemoteRebaseText = "Choose remote/incoming (ours)";
    public const string TakeOnlyRemoteRebaseTooltip = "Take only the changes from the branch you are rebasing";
    public const string RemoteRebaseTooltip = "Changes from the branch you are rebasing";
    public const string ChooseLocalMergeText = "Choose local/current (ours)";
    public const string TakeOnlyLocalMergeTooltip = "Take only the changes from the current branch";
    public const string LocalMergeTooltip = "Changes from the current branch";
    public const string ChooseRemoteMergeText = "Choose remote/incoming (theirs)";
    public const string TakeOnlyRemoteMergeTooltip = "Take only the changes from the branch you are merging";
    public const string RemoteMergeTooltip = "Changes from the branch you are merging";
    public const string ChooseBaseTooltip = "Take no changes and revert to base content!";
    public const string NoBaseFileMergeCaption = "Merge";
    public const string ChooseBaseFileFailed = "Choose base file failed.";
    public const string ChooseLocalFileFailed = "Choose local file failed.";
    public const string ChooseRemoteFileFailed = "Choose remote file failed.";
    public const string ErrorStartingMergetool = "Error starting mergetool: {0}";
    public const string StageFilename = "Stage '{0}'";
    public const string AskMergeConflictSolved = "Is the merge conflict solved?";
    public const string AskMergeConflictSolvedCaption = "Conflict solved?";
    public const string AllConflictsResolvedCaption = "Commit";
    public const string ResetCaption = "Reset";
    public const string AreYouSureYouWantDeleteFilesCaption = "WARNING!";
    public const string FailureWhileOpenFile = "Open temporary file failed.";
    public const string FailureWhileSaveFile = "Save file failed.";
    public const string Local = "LOCAL";
    public const string Remote = "REMOTE";
    public const string Base = "BASE";

    public static readonly string NoMergeTool =
        "There is no mergetool configured." + Environment.NewLine + "Please go to settings and set a mergetool!";

    public static readonly string NoMergeToolConfigured =
        "The mergetool is not correctly configured." + Environment.NewLine + "Please go to settings and configure the mergetool!";

    public static readonly string AllConflictsResolved =
        "All merge conflicts are resolved, you can commit." + Environment.NewLine + "Do you want to commit now?";

    public static readonly string FileIsBinary =
        "The selected file appears to be a binary file." + Environment.NewLine + "Are you sure you want to open this file in {0}?";

    public static readonly string NoBaseRevision =
        "There is no base revision for '{0}'." + Environment.NewLine + "Fall back to 2-way merge?";

    public static readonly string FileBinaryChooseLocalBaseRemote =
        "File '{0}' appears to be binary." + Environment.NewLine + "Choose to keep the local '{1}', remote '{2}' or base file.";

    public static readonly string FileChangeLocallyAndRemotely =
        "The file has been changed both locally ({0}) and remotely ({1}). Merge the changes.";

    public static readonly string FileCreatedLocallyAndRemotely =
        "A file with the same name has been created locally ({0}) and remotely ({1}). Choose the file you want to keep or merge the files.";

    public static readonly string FileCreatedLocallyAndRemotelyLong =
        "File '{0}' does not have a base revision." + Environment.NewLine +
        "A file with the same name has been created locally ({1}) and remotely ({2}) causing this conflict." +
        Environment.NewLine + Environment.NewLine + "Choose the file you want to keep, merge the files or delete the file?";

    public static readonly string FileDeletedLocallyAndModifiedRemotely =
        "The file has been deleted locally ({0}) and modified remotely ({1}). Choose to delete the file or keep the modified version.";

    public static readonly string FileDeletedLocallyAndModifiedRemotelyLong =
        "File '{0}' does not have a local revision." + Environment.NewLine +
        "The file has been deleted locally ({1}) but modified remotely ({2})." + Environment.NewLine + Environment.NewLine +
        "Choose to delete the file or keep the modified version.";

    public static readonly string FilesDeletedLocallyAndModifiedRemotelyLong =
        "'{0}' and {1} other out of {2} selected files do not have a local revision." + Environment.NewLine +
        "The files have been deleted locally, but modified remotely" + Environment.NewLine + Environment.NewLine +
        "Choose to delete the files or keep the modified versions.";

    public static readonly string FilesDeletedLocallyAndModifiedRemotelyLongNoOtherFilesSelected =
        "'{0}' and {1} other selected file(s) do not have a local revision." + Environment.NewLine +
        "The files have been deleted locally, but modified remotely" + Environment.NewLine + Environment.NewLine +
        "Choose to delete the files or keep the modified versions.";

    public static readonly string FileModifiedLocallyAndDeletedRemotely =
        "The file has been modified locally ({0}) and deleted remotely ({1}). Choose to delete the file or keep the modified version.";

    public static readonly string FileModifiedLocallyAndDeletedRemotelyLong =
        "File '{0}' does not have a remote revision." + Environment.NewLine +
        "The file has been modified locally ({1}) but deleted remotely ({2})." + Environment.NewLine + Environment.NewLine +
        "Choose to delete the file or keep the modified version.";

    public static readonly string FilesModifiedLocallyAndDeletedRemotelyLong =
        "'{0}' and {1} other out of {2} selected files do not have a remote revision." + Environment.NewLine +
        "The files have been modified locally, but deleted remotely." + Environment.NewLine + Environment.NewLine +
        "Choose to delete the files or keep the modified versions.";

    public static readonly string FilesModifiedLocallyAndDeletedRemotelyLongNoOtherFilesSelected =
        "'{0}' and {1} other selected file(s) do not have a remote revision." + Environment.NewLine +
        "The files have been modified locally, but deleted remotely." + Environment.NewLine + Environment.NewLine +
        "Choose to delete the files or keep the modified versions.";

    public static readonly string AbortCurrentOperation =
        "You can abort the current conflict resolution by resetting hard." + Environment.NewLine +
        "All changes since the last commit will be deleted." + Environment.NewLine + Environment.NewLine +
        "Do you want to reset the changes?";

    public static readonly string AreYouSureYouWantDeleteFiles =
        "Are you sure you want to DELETE all changes?" + Environment.NewLine + Environment.NewLine +
        "This action cannot be made undone.";

    private const string Kdiff3 = "kdiff3";
    private const string Kdiff3Command = "\"$BASE\" \"$LOCAL\" \"$REMOTE\" -o \"$MERGED\"";
    private const string ExecutablePattern = ".exe";

    /// <param name="inRebase">Upstream's <c>InTheMiddleOfRebase</c>: during a rebase ours and theirs swap.</param>
    public ConflictsDialog(bool inRebase)
    {
        InRebase = inRebase;
    }

    public bool InRebase { get; }

    /// <summary>
    ///  Upstream's <c>GetLocalSideString</c>: the local side is theirs during a rebase.
    /// </summary>
    public string LocalSide => InRebase ? Theirs : Ours;

    public string RemoteSide => InRebase ? Ours : Theirs;

    public string ChooseLocalText => InRebase ? ChooseLocalRebaseText : ChooseLocalMergeText;

    public string ChooseLocalTooltip => InRebase ? TakeOnlyLocalRebaseTooltip : TakeOnlyLocalMergeTooltip;

    public string ChooseRemoteText => InRebase ? ChooseRemoteRebaseText : ChooseRemoteMergeText;

    public string ChooseRemoteTooltip => InRebase ? TakeOnlyRemoteRebaseTooltip : TakeOnlyRemoteMergeTooltip;

    public string LocalLabelTooltip => InRebase ? LocalRebaseTooltip : LocalMergeTooltip;

    public string RemoteLabelTooltip => InRebase ? RemoteRebaseTooltip : RemoteMergeTooltip;

    /// <summary>
    ///  Upstream's labels with the side in parentheses (<c>UpdateSuffixWithinParenthesis</c>).
    /// </summary>
    public string LocalLabelText(string label) => label.UpdateSuffixWithinParenthesis(LocalSide);

    public string RemoteLabelText(string label) => label.UpdateSuffixWithinParenthesis(RemoteSide);

    /// <summary>
    ///  Upstream's <c>HandleSingleSelect</c> description: which sides changed, created or deleted the file; null for a case
    ///  upstream leaves the previous text for.
    /// </summary>
    public string? Description(ConflictData item, Func<string, string>? translate = null)
    {
        translate ??= text => text;
        return (HasFile(item.Base), HasFile(item.Local), HasFile(item.Remote)) switch
        {
            (true, true, true) => string.Format(translate(FileChangeLocallyAndRemotely), LocalSide, RemoteSide),
            (false, true, true) => string.Format(translate(FileCreatedLocallyAndRemotely), LocalSide, RemoteSide),
            (true, false, true) => string.Format(translate(FileDeletedLocallyAndModifiedRemotely), LocalSide, RemoteSide),
            (true, true, false) => string.Format(translate(FileModifiedLocallyAndDeletedRemotely), LocalSide, RemoteSide),
            _ => null,
        };
    }

    /// <summary>
    ///  Upstream's base, local and remote names: "no base" or "deleted" for a missing side, and a submodule's commit
    ///  (<c>GetShortHash</c>).
    /// </summary>
    public static (string Base, string Local, string Remote) Names(ConflictData item, bool isSubmodule,
        Func<string, string>? translate = null)
    {
        translate ??= text => text;
        string baseName = HasFile(item.Base) ? item.Base.Filename : translate(NoBase);
        string localName = HasFile(item.Local) ? item.Local.Filename : translate(Deleted);
        string remoteName = HasFile(item.Remote) ? item.Remote.Filename : translate(Deleted);
        if (isSubmodule)
        {
            baseName += ShortHash(item.Base);
            localName += ShortHash(item.Local);
            remoteName += ShortHash(item.Remote);
        }

        return (baseName, localName, remoteName);

        string ShortHash(ConflictedFileData side)
            => $"@{(side.ObjectId.IsZero ? translate(Deleted) : side.ObjectId.ToShortString())}";
    }

    public static bool HasFile(ConflictedFileData side) => !string.IsNullOrEmpty(side.Filename);

    /// <summary>
    ///  Upstream's <c>GetConflicts</c>: the selected files deleted locally and modified remotely, those modified locally and
    ///  deleted remotely, and the rest, each in reverse selection order as upstream inserts them.
    /// </summary>
    public static (IReadOnlyList<ConflictData> DeletedLocally, IReadOnlyList<ConflictData> DeletedRemotely,
        IReadOnlyList<ConflictData> Remaining) Classify(IEnumerable<ConflictData> selected)
    {
        List<ConflictData> deletedLocally = [];
        List<ConflictData> deletedRemotely = [];
        List<ConflictData> remaining = [];
        foreach (ConflictData item in selected)
        {
            if (!HasFile(item.Local) && HasFile(item.Remote))
            {
                deletedLocally.Insert(0, item);
            }
            else if (HasFile(item.Local) && !HasFile(item.Remote))
            {
                deletedRemotely.Insert(0, item);
            }
            else
            {
                remaining.Insert(0, item);
            }
        }

        return (deletedLocally, deletedRemotely, remaining);
    }

    /// <summary>
    ///  Upstream's <c>CheckForLocalRevision</c> and <c>CheckForRemoteRevision</c> texts for a file deleted on one side:
    ///  <paramref name="unsolved"/> such files are left including this one, of <paramref name="selected"/> selected;
    ///  <paramref name="othersSelected"/> says whether files of other kinds are selected too. Empty once "apply to all" is
    ///  chosen, as upstream.
    /// </summary>
    public string DeletedSideText(ConflictData item, bool deletedLocally, int unsolved, int selected, bool othersSelected,
        bool applyToAll, Func<string, string>? translate = null)
    {
        translate ??= text => text;
        if (applyToAll)
        {
            return "";
        }

        if (unsolved == 1)
        {
            return string.Format(translate(deletedLocally ? FileDeletedLocallyAndModifiedRemotelyLong
                : FileModifiedLocallyAndDeletedRemotelyLong), item.Filename, LocalSide, RemoteSide);
        }

        if (unsolved > 1)
        {
            return othersSelected
                ? string.Format(translate(deletedLocally ? FilesDeletedLocallyAndModifiedRemotelyLong
                    : FilesModifiedLocallyAndDeletedRemotelyLong), item.Filename, unsolved - 1, selected)
                : string.Format(translate(deletedLocally ? FilesDeletedLocallyAndModifiedRemotelyLongNoOtherFilesSelected
                    : FilesModifiedLocallyAndDeletedRemotelyLongNoOtherFilesSelected), item.Filename, unsolved - 1);
        }

        return "";
    }

    /// <summary>
    ///  Upstream's command link texts of the "Solve merge conflict" dialog for a file deleted on one side: local, remote and
    ///  base.
    /// </summary>
    public (string Local, string Remote, string Base) DeletedSideButtons(bool deletedLocally,
        Func<string, string>? translate = null)
    {
        translate ??= text => text;
        return deletedLocally
            ? ($"{translate(DeleteFileButtonText)} ({LocalSide})", $"{translate(KeepModifiedButtonText)} ({RemoteSide})",
                $"{translate(KeepBaseButtonText)} ({LocalSide})")
            : ($"{translate(KeepModifiedButtonText)} ({LocalSide})", $"{translate(DeleteFileButtonText)} ({RemoteSide})",
                translate(KeepBaseButtonText));
    }

    /// <summary>
    ///  Upstream's <c>InitMergetool</c>: <c>merge.guitool</c> when git supports it, else <c>merge.tool</c>, with its
    ///  <c>mergetool.&lt;tool&gt;.cmd</c> and <c>.path</c> (kdiff3 needs neither); on Windows a command that names an .exe
    ///  gives the program; the program must be found.
    /// </summary>
    /// <param name="setting">Git's effective setting of a key; empty when not set.</param>
    /// <param name="supportsGuiTool">Upstream's <c>SupportGuiMergeTool</c> of the installed git.</param>
    /// <param name="tryFindFullPath">Upstream's <c>PathUtil.TryFindFullPath</c>.</param>
    public static MergeTool ReadMergeTool(Func<string, string> setting, bool supportsGuiTool, bool isWindows,
        TryFind tryFindFullPath, Func<string, string>? translate = null)
    {
        translate ??= text => text;
        string tool = supportsGuiTool ? setting("merge.guitool") : "";
        if (string.IsNullOrEmpty(tool))
        {
            tool = setting("merge.tool");
        }

        if (string.IsNullOrEmpty(tool))
        {
            return new MergeTool(null, null, null, translate(NoMergeTool));
        }

        string command = setting($"mergetool.{tool}.cmd");
        string path = setting($"mergetool.{tool}.path");
        if (tool == Kdiff3)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = Kdiff3;
            }

            if (string.IsNullOrEmpty(command))
            {
                command = Kdiff3Command;
            }
        }

        if (isWindows)
        {
            int index = command.IndexOf(ExecutablePattern, StringComparison.Ordinal);
            if (index >= 0)
            {
                path = command[..(index + ExecutablePattern.Length + 1)].Trim('"', ' ');
                command = command[(index + ExecutablePattern.Length + 1)..];
            }
        }

        return tryFindFullPath(path, out string? fullPath)
            ? new MergeTool(tool, command, fullPath, null)
            : new MergeTool(tool, command, path, translate(NoMergeToolConfigured));
    }

    /// <summary>
    ///  Upstream's <c>PathUtil.TryFindFullPath</c> shape.
    /// </summary>
    public delegate bool TryFind(string fileName, out string? fullPath);

    /// <summary>
    ///  Upstream's <c>Use2WayMerge</c>: the base file left out of the command for the tools that can merge two files.
    /// </summary>
    public static string TwoWayMergeArguments(string tool, string arguments)
        => tool.ToLowerInvariant() switch
        {
            "kdiff3" or "diffmerge" or "smerge" => arguments.Replace("\"$BASE\"", ""),
            "tortoisemerge" => arguments.Replace("-base:\"$BASE\"", "").Replace("/base:\"$BASE\"", "")
                .Replace("mine:\"$LOCAL\"", "base:\"$LOCAL\""),
            _ => arguments,
        };

    /// <summary>
    ///  Upstream's substitution of the checked out files and the merged file into the tool's command.
    /// </summary>
    public static string MergeToolArguments(string arguments, string? baseFile, string? localFile, string? remoteFile,
        string mergedFile)
        => arguments.Replace("$BASE", baseFile).Replace("$LOCAL", localFile).Replace("$REMOTE", remoteFile)
            .Replace("$MERGED", mergedFile);

    /// <summary>
    ///  Upstream's check of the tool's exit code and the merged file's time: a success that changed the file is staged; an
    ///  exit code 1 that changed it, or a success that did not, is asked about.
    /// </summary>
    public static MergeToolOutcome Outcome(int? exitCode, bool fileChanged)
        => exitCode == 0 && fileChanged ? MergeToolOutcome.Stage
            : (exitCode == 1 && fileChanged) || (exitCode == 0 && !fileChanged) ? MergeToolOutcome.Ask
            : MergeToolOutcome.None;
}
