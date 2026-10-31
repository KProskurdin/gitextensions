using GitCommands.Git;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's Repository > Git maintenance menu (FormBrowse's <c>CompressGitDatabaseToolStripMenuItemClick</c>,
///  <c>recoverLostObjectsToolStripMenuItemClick</c>, <c>deleteIndexLockToolStripMenuItem_Click</c> and
///  <c>EditLocalGitConfigToolStripMenuItemClick</c>).
/// </summary>
public partial class MainWindow
{
    // Upstream's text.
    public const string IndexLockCantDelete = "Failed to delete index.lock";

    // Upstream's argument for "Compress git database".
    private const string CompressGitDatabaseArguments = "gc";

    private void WireMaintenanceMenu()
    {
        CompressGitDatabaseMenuItem.Click += (_, _) => RunOnRepositoryFolder(path =>
            Run(() => RunGitWithOutputAsync(path, CompressGitDatabaseArguments)));
        RecoverLostObjectsMenuItem.Click +=
            (_, _) => RunOnRepositoryFolder(path => Run(() => VerifyDatabaseAsync(path)));
        DeleteIndexLockMenuItem.Click += (_, _) => RunOnRepositoryFolder(path => Run(() => DeleteIndexLockAsync(path)));
        EditLocalGitConfigMenuItem.Click += (_, _) => RunOnRepositoryFolder(path => Run(() =>
            new EditorWindow(GitModules.Open(path).ResolveGitInternalPath("config"), showWarning: true)
                .ShowDialog(this)));
    }

    // Upstream's StartVerifyDatabaseDialog: the repository is read again afterwards, as recovering adds tags.
    private async Task VerifyDatabaseAsync(string path)
    {
        await NewVerifyDatabaseWindow(path).ShowDialog(this);
        RefreshRepository();
    }

    // FormVerify's create tag and create branch windows on a lost commit.
    private VerifyDatabaseWindow NewVerifyDatabaseWindow(string path)
        => new(path, arguments => RunGitWithOutputAsync(path, arguments),
            (owner, id) => PromptCreateTagAsync(id.ToString(), owner),
            (owner, id) => new CreateBranchWindow(path, _actions, _preferences, id.ToString()).ShowDialog<bool>(owner));

    // Upstream deletes the index.lock files of the repository and its submodules, and shows the file that would not go.
    private async Task DeleteIndexLockAsync(string path)
    {
        try
        {
            await Task.Run(() => GitModules.Open(path).UnlockIndex(includeSubmodules: true));
        }
        catch (FileDeleteException ex)
        {
            await new ErrorWindow($"{UpstreamTranslation.ByText(IndexLockCantDelete)}\n\n{ex.FileName}")
                .ShowDialog(this);
        }
    }
}
