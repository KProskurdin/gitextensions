using Avalonia.Controls;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's smaller menu commands: Commands > Undo last commit... (FormBrowse's
///  <c>undoLastCommitToolStripMenuItem_Click</c>) and Clean working directory... (<c>FormCleanupRepository</c>), Tools >
///  Git GUI and GitK (<c>ToolsToolStripMenuItem</c>), and
///  Repository > Edit .gitattributes and Edit .mailmap (<c>FormGitAttributes</c>, <c>FormMailMap</c>).
/// </summary>
public partial class MainWindow
{
    private void WireCommandsMenu()
    {
        UndoLastCommitMenuItem.Click += (_, _) => RunOnRepository(UndoLastCommitAsync);
        ArchiveMenuItem.Click += (_, _) => ArchiveSelectedRevision();
        CherryPickCommandMenuItem.Click += (_, _) => CherryPickSelectedRevision();
        FormatPatchMenuItem.Click += (_, _) => RunOnRepositoryFolder(path =>
            Run(() => new FormatPatchWindow(path, _preferences).ShowDialog(this)));
        ApplyPatchMenuItem.Click += (_, _) => RunOnRepositoryFolder(path =>
            Run(() => new ApplyPatchWindow(path, _preferences, arguments => RunGitWithOutputAsync(path, arguments),
                ShowConflicts, RefreshRepository).ShowDialog(this)));
        ViewPatchMenuItem.Click += (_, _) => RunOnRepositoryFolder(path =>
            Run(() => new ViewPatchWindow(path, patchFile: null).ShowDialog(this)));
        ArchiveCommitMenuItem.Click += (_, _) => ArchiveSelectedRevision();
        CleanupMenuItem.Click += (_, _) => RunOnRepositoryFolder(path =>
            Run(() => new CleanupRepositoryWindow(path, RefreshRepository).ShowDialog(this)));
        GitGuiMenuItem.Click += (_, _) => RunOnRepositoryFolder(path => GitModules.Open(path).RunGui());
        GitKMenuItem.Click += (_, _) => RunOnRepositoryFolder(path => GitModules.Open(path).RunGitK());
        GitAttributesMenuItem.Click += (_, _) =>
            RunOnRepository(path => EditRepositoryFileAsync(path, RepositoryTextFiles.GitAttributes));
        MailMapMenuItem.Click += (_, _) =>
            RunOnRepository(path => EditRepositoryFileAsync(path, RepositoryTextFiles.MailMap));
        WireMaintenanceMenu();
    }

    // Upstream's undoLastCommitToolStripMenuItem_Click: after its suppressible question, a soft reset to HEAD~1 keeps
    // the commit's changes staged.
    private async Task<bool> UndoLastCommitAsync(string path)
        => await ConfirmAsync(Confirmation.UndoLastCommit, Confirmations.UndoLastCommitQuestion,
               UpstreamTranslation.ByText(Confirmations.UndoLastCommitCaption), Confirmations.UndoLastCommitCaption)
           && await _actions.ResetAsync(path, "HEAD~1", ResetMode.Soft);

    // Upstream's archiveToolStripMenuItem_Click and the grid's ArchiveRevision: the selected commit (the grid selects
    // one, so no second revision), archived by git with its output shown, as upstream's FormProcess.
    private void ArchiveSelectedRevision()
    {
        if (RepositoryPath is not { } path || _commits.Selected is not { } row || ArtificialCommits.IsArtificial(row.Hash))
        {
            return;
        }

        Run(() => new ArchiveWindow(path, row.Hash, diffRevision: null, path: null, _preferences,
                arguments => _actions.RunProgramAsync("git archive", AppSettings.GitCommand, arguments, path))
            .ShowDialog(this));
    }

    // Upstream's StartCherryPickDialog for the selected commit (the grid selects one).
    private void CherryPickSelectedRevision()
    {
        if (RepositoryPath is not { } path || _commits.Selected is not { } row || ArtificialCommits.IsArtificial(row.Hash))
        {
            return;
        }

        Run(() => new CherryPickWindow(path, row.Hash, _preferences,
                (owner, commit, arguments) => CherryPickAsync(owner, path, commit, arguments))
            .ShowDialog(this));
    }

    // Upstream's btnPick_Click: the command, then MergeConflictHandler.HandleMergeConflicts, which offers the
    // conflicts window when the pick stopped on conflicts.
    private async Task CherryPickAsync(Window owner, string path, string commit, ArgumentString arguments)
    {
        if (await _actions.CherryPickAsync(path, commit, arguments)
            || !await Task.Run(() => GitModules.Open(path).InTheMiddleOfConflictedMerge()))
        {
            return;
        }

        if (await ConfirmWindow.AskAsync(owner, _preferences, Confirmation.ResolveConflicts,
                UpstreamTranslation.ByText(Confirmations.ResolveConflictsQuestion), "Yes",
                UpstreamTranslation.ByText(Confirmations.ResolveConflictsCaption)))
        {
            ShowConflicts();
        }
    }

    // Upstream's FormProcess for a command of a dialog: git with its output shown; the repository is read again after.
    private Task<bool> RunGitWithOutputAsync(string path, string arguments)
        => _actions.RunProgramAsync("git " + arguments.Split(' ')[0], AppSettings.GitCommand, arguments, path);

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
