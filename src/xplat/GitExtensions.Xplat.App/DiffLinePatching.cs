using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream FileViewer's line patching (<c>SupportLinePatching</c>, <c>StageSelectedLines</c>,
///  <c>UnstageSelectedLines</c>, <c>ResetSelectedLines</c>, <c>CherryPickAllChanges</c>) for the windows that show one file's
///  diff: which actions a file's diff offers, and the actions on the lines selected in a <see cref="DiffView"/>.
/// </summary>
internal static class DiffLinePatching
{
    // Upstream's texts.
    public const string ResetSelectedLinesConfirmation =
        "Are you sure you want to reset the changes to the selected lines?";

    public const string ResetChangesCaption = "Reset changes";

    /// <summary>
    ///  Upstream's <c>SupportLinePatching</c> for a file shown between two revisions (<paramref name="second"/> the working
    ///  directory's or the index's id for those) with git's status letter: lines of an edited or untracked working directory
    ///  file are staged, of an edited or added index file unstaged; a commit's lines are applied when the file is in the
    ///  working directory, or the commit adds it.
    /// </summary>
    public static DiffLineAction For(string repositoryPath, string path, string status, ObjectId second)
    {
        if (second == ObjectId.WorkTreeId)
        {
            return status is "M" or "?" ? DiffLineAction.Stage : DiffLineAction.None;
        }

        if (second == ObjectId.IndexId)
        {
            return status is "M" or "A" ? DiffLineAction.Unstage : DiffLineAction.None;
        }

        return second.IsArtificial || status == "D"
            ? DiffLineAction.None
            : status == "A" || File.Exists(Path.Combine(repositoryPath, path))
                ? DiffLineAction.Apply
                : DiffLineAction.None;
    }

    /// <summary>
    ///  The diff's line hotkey or menu item (<see cref="DiffView.LinesHotkey"/>) for the shown file, as upstream's
    ///  <c>ExecuteCommand</c> runs it: stage, unstage or reset the working directory's and the index's lines (reset after
    ///  upstream's question), apply or undo a commit's lines. False when the diff does not offer it. After a working
    ///  directory or index change <paramref name="reload"/> shows the file's diff again.
    /// </summary>
    public static async Task<bool> RunAsync(Window owner, DiffView view, DiffCommand command,
        FileListMenuServices services,
        string repositoryPath, string status, Func<Task> reload)
    {
        if (view.SelectedRange() is not { } range)
        {
            return false;
        }

        RepositoryOperationsViewModel actions = services.Actions;
        switch (view.LineAction, command)
        {
            case (DiffLineAction.Stage, DiffCommand.StageLines):
                if (await actions.StageLinesAsync(repositoryPath, range.Text, range.Start, range.Length,
                        unstage: false))
                {
                    await reload();
                }

                return true;
            case (DiffLineAction.Unstage, DiffCommand.UnstageLines):
                if (await actions.StageLinesAsync(repositoryPath, range.Text, range.Start, range.Length, unstage: true,
                        isNewFile: status == "A"))
                {
                    await reload();
                }

                return true;
            case (DiffLineAction.Stage or DiffLineAction.Unstage, DiffCommand.ResetLines):
                if (await ResetAsync(owner, actions, repositoryPath, range,
                        staged: view.LineAction == DiffLineAction.Unstage))
                {
                    await reload();
                }

                return true;
            case (DiffLineAction.Apply, DiffCommand.StageLines or DiffCommand.ResetLines):
                await ApplyAsync(owner, services, repositoryPath, range, reverse: command == DiffCommand.ResetLines,
                    status);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    ///  Upstream's <c>ResetNoncommittedSelectedLines</c>: after upstream's question, the lines are undone; false when the
    ///  user declined or git failed.
    /// </summary>
    public static async Task<bool> ResetAsync(Window owner, RepositoryOperationsViewModel actions,
        string repositoryPath,
        (string Text, int Start, int Length) range, bool staged)
        => await new ConfirmWindow(UpstreamTranslation.ByText(ResetSelectedLinesConfirmation), "Yes",
                   UpstreamTranslation.ByText(ResetChangesCaption), cancelText: "No")
               .ShowDialog<bool>(owner)
           && await actions.ResetLinesAsync(repositoryPath, range.Text, range.Start, range.Length, staged);

    /// <summary>
    ///  Upstream's <c>ApplySelectedLines</c> for a commit's diff, and its <c>HandleMergeConflicts</c> when git's three-way
    ///  merge left conflicts.
    /// </summary>
    public static async Task ApplyAsync(Window owner, FileListMenuServices services, string repositoryPath,
        (string Text, int Start, int Length) range, bool reverse, string status)
    {
        if (!await services.Actions.ApplyLinesAsync(repositoryPath, range.Text, range.Start, range.Length, reverse,
                isNewFile: status == "A", isRenamed: status == "R")
            && services.OfferConflicts is { } offerConflicts)
        {
            await offerConflicts(owner, repositoryPath);
        }
    }
}
