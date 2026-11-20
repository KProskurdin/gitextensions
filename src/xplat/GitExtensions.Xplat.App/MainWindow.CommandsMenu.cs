using Avalonia.Controls;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's smaller menu commands: Commands > Undo last commit... (FormBrowse's
///  <c>undoLastCommitToolStripMenuItem_Click</c>) and Clean working directory... (<c>FormCleanupRepository</c>), Tools >
///  Git GUI, GitK and PuTTY (<c>ToolsToolStripMenuItem</c>), and
///  Repository > Edit .gitattributes and Edit .mailmap (<c>FormGitAttributes</c>, <c>FormMailMap</c>), Sparse Working Copy
///  (<c>FormSparseWorkingCopy</c>) and Repository settings... (<c>StartRepoSettingsDialog</c>).
/// </summary>
public partial class MainWindow
{
    // Upstream's StartRepoSettingsDialog opens the settings on this page.
    private const string GitConfigSettingsPage = "GitConfigSettingsPage";

    // Upstream's text.
    private const string PuttyNotInstalled = "Is PuTTY installed?";

    // Upstream's StartApplyPatchDialog.
    private void ShowApplyPatch(string path)
        => Run(() => new ApplyPatchWindow(path, _preferences, arguments => RunGitWithOutputAsync(path, arguments),
            ShowConflicts, RefreshRepository).ShowDialog(this));

    private void WireCommandsMenu()
    {
        // The items' actions are the toolbar view model's commands (bound in XAML); PuTTY is Windows'.
        PuttySeparator.IsVisible = PuttyMenuItem.IsVisible = OperatingSystem.IsWindows();
    }

    // Upstream's undoLastCommitToolStripMenuItem_Click: after its suppressible question, a soft reset to HEAD~1 keeps
    // the commit's changes staged.
    private async Task<bool> UndoLastCommitAsync(string path)
        => await ConfirmAsync(Confirmation.UndoLastCommit, Confirmations.UndoLastCommitQuestion,
               UpstreamTranslation.ByText(Confirmations.UndoLastCommitCaption), Confirmations.UndoLastCommitCaption)
           && await _actions.ResetAsync(path, "HEAD~1", ResetMode.Soft);

    // Upstream's archiveToolStripMenuItem_Click and the grid's ArchiveRevision: the selected commit, archived by git with
    // its output shown, as upstream's FormProcess.
    private void ArchiveSelectedRevision()
    {
        if (RepositoryPath is not { } path || _commits.Selected is not { } row || ArtificialCommits.IsArtificial(row.Hash))
        {
            return;
        }

        // Upstream's ArchiveRevisionToolStripMenuItemClick: the latest selected commit, and the other one of two selected
        // for an archive of the files changed between them.
        IReadOnlyList<CommitRow> selected = SelectedRevisions;
        if (selected.Count > 2)
        {
            _ = new MessageBoxWindow(UpstreamTranslation.ByText(ArchiveDialog.SelectOneOrTwo),
                UpstreamTranslation.ByText(ArchiveDialog.ArchiveRevisionCaption), MessageBoxButtons.OK).ShowDialog(this);
            return;
        }

        string? diffRevision = selected.Count == 2 ? selected[1].Hash : null;
        Run(() => ArchiveAsync(this, path, row.Hash, diffRevision, filePath: null));
    }

    // Upstream's StartArchiveDialog: a commit, the one its changed files are taken against, and a path to archive.
    private Task ArchiveAsync(Window owner, string path, string revision, string? diffRevision, string? filePath)
        => new ArchiveWindow(path, revision, diffRevision, filePath, _preferences,
                arguments => _actions.RunProgramAsync("git archive", AppSettings.GitCommand, arguments, path))
            .ShowDialog(owner);

    // Upstream's CherryPickCommitToolStripMenuItemClick and StartCherryPickDialog(revisions): a cherry pick window for each
    // selected commit, the lowest in the grid first; the options carry over (they are saved on each pick), and the first
    // window closed without a pick ends the run.
    private void CherryPickSelectedRevision()
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        List<CommitRow> rows =
        [
            .. SelectedRevisionsInGridOrder(descending: true).Where(row => !ArtificialCommits.IsArtificial(row.Hash))
        ];
        if (rows.Count == 0)
        {
            return;
        }

        Run(() => CherryPickRevisionsAsync(this, path, [.. rows.Select(row => row.Hash)]));
    }

    // Upstream's StartCherryPickDialog(revisions); true when every commit was picked.
    private async Task<bool> CherryPickRevisionsAsync(Window owner, string path, IReadOnlyList<string?> revisions)
    {
        foreach (string? revision in revisions)
        {
            if (!await new CherryPickWindow(path, revision, _preferences,
                        (window, commit, arguments) => CherryPickAsync(window, path, commit, arguments))
                    .ShowDialog<bool>(owner))
            {
                return false;
            }
        }

        return true;
    }

    // Upstream's btnPick_Click: the command, then MergeConflictHandler.HandleMergeConflicts, which offers the
    // conflicts window when the pick stopped on conflicts.
    private async Task CherryPickAsync(Window owner, string path, string commit, ArgumentString arguments)
    {
        if (!await _actions.CherryPickAsync(path, commit, arguments))
        {
            await OfferToResolveConflictsAsync(owner, path);
        }
    }

    // Upstream's StartRevertCommitDialog for the selected commit (the grid selects one).
    private void RevertSelectedRevision()
    {
        if (RepositoryPath is not { } path || _commits.Selected is not { } row || ArtificialCommits.IsArtificial(row.Hash))
        {
            return;
        }

        Run(() => RevertCommitAsync(this, path, row.Hash));
    }

    // Upstream's StartRevertCommitDialog for a commit.
    private Task RevertCommitAsync(Window owner, string path, string revision)
        => new RevertCommitWindow(path, revision, async (window, arguments, _) =>
            {
                // As upstream's Revert_Click: the result is not checked, a failure is most likely a conflict.
                await RunGitWithOutputAsync(path, arguments.ToString());
                await OfferToResolveConflictsAsync(window, path);
            })
            .ShowDialog(owner);

    // Upstream's MergeConflictHandler.HandleMergeConflicts: when the command stopped on conflicts, the question, then the
    // conflicts window.
    private async Task OfferToResolveConflictsAsync(Window owner, string path)
    {
        if (await Task.Run(() => GitModules.Open(path).InTheMiddleOfConflictedMerge())
            && await ConfirmWindow.AskAsync(owner, _preferences, Confirmation.ResolveConflicts,
                UpstreamTranslation.ByText(Confirmations.ResolveConflictsQuestion), "Yes",
                UpstreamTranslation.ByText(Confirmations.ResolveConflictsCaption)))
        {
            ShowConflicts();
        }
    }

    // Upstream's FormProcess for a command of a dialog: git with its output shown; the repository is read again after.
    private Task<bool> RunGitWithOutputAsync(string path, string arguments)
        => _actions.RunProgramAsync("git " + arguments.Split(' ')[0], AppSettings.GitCommand, arguments, path);

    // Upstream's PuttyHelpers.StartPageant and StartPuttygen: the program in the repository, or upstream's error when it
    // is not where the settings say.
    private void StartPuttyProgram(string program) => RunOnRepositoryFolder(path =>
    {
        if (!File.Exists(program))
        {
            string installed =
                UpstreamTranslation.Text("TranslatedStrings", "_errorSshPuTTYInstalled", PuttyNotInstalled);
            _ = new ErrorWindow($"'{program}'\n\n{installed}").ShowDialog(this);
            return;
        }

        new Executable(program, path).Start().Dispose();
    });

    // Upstream's StartGitAttributesDialog and StartMailMapDialog; a saved .mailmap changes the authors the grid shows,
    // so the repository is read again, as upstream's RepoChangedNotifier does.
    private async Task<bool> EditRepositoryFileAsync(string path, RepositoryTextFile file)
    {
        bool saved = await new RepositoryTextFileWindow(path, file).ShowDialog<bool>(this);
        if (saved && file.NotifiesRepositoryChanged)
        {
            RefreshRepository();
        }

        return saved;
    }
}
