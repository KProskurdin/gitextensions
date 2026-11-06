using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's "Find in commit files using git-grep" in the Diff tab (RevisionDiffControl sets the list's
///  <c>CanUseFindInCommitFilesGitGrep</c>): with a search, the tab lists the selected commit's files in upstream's diff
///  groups and, last, the "grep" group of the files git-grep matched (<c>FileStatusDiffCalculator</c>), whose matches the
///  viewer shows.
/// </summary>
public partial class MainWindow
{
    // The Diff tab's git-grep argument ("" for no search), kept as the selection changes, as upstream's.
    private string _gitGrep = "";
    private GitGrepBox? _diffGitGrep;

    /// <summary>
    ///  The Diff tab's git-grep box, for tests.
    /// </summary>
    public GitGrepBox? DiffGitGrep => _diffGitGrep;

    // Upstream's ExpandIfFewFiles limit: a filtered or searched list is shown expanded while it has fewer files than this.
    private const int MaxExpandedFiles = 100;

    // The File tree tab's git-grep argument ("" for no search): its list shows the files the search matched.
    private string _fileTreeGrep = "";
    private GitGrepBox? _fileTreeGitGrep;

    /// <summary>
    ///  The File tree tab's git-grep box, for tests.
    /// </summary>
    public GitGrepBox? FileTreeGitGrep => _fileTreeGitGrep;

    // The File tree tab's list has no toolbar in upstream's file tree mode: the box, the window and the menu items only.
    private void WireFileTreeGitGrep()
        => _fileTreeGitGrep = new GitGrepBox(this, FileTreeGitGrepBox, FileTreeGitGrepDeleteButton, button: null,
            _preferences, argument =>
            {
                _fileTreeGrep = argument;
                _fileTreeHash = null;
                ShowFileTreeIfVisible();
            });

    // Upstream FileStatusList's OnVisibleChanged: the list that shows follows the setting both lists share.
    private void ShowGitGrepBoxOfTab()
    {
        if (DetailTabs.SelectedItem == DiffTab)
        {
            _diffGitGrep?.SetBoxVisible(_preferences.ShowFindInCommitFilesGitGrep);
        }
        else if (DetailTabs.SelectedItem == FileTreeTab)
        {
            _fileTreeGitGrep?.SetBoxVisible(_preferences.ShowFindInCommitFilesGitGrep);
        }
    }

    // A file's matches at the File tree tab's commit (or HEAD).
    private static Task<(IReadOnlyList<GrepLine> Lines, string? Error)> TreeFileGrepAsync(string path, string revision,
        string file, string grep, DiffOptions options)
        => Task.Run(async () =>
        {
            GitModule module = GitModules.Open(path);
            ObjectId? id = ObjectId.TryParse(revision, out ObjectId parsed) ? parsed : module.RevParse(revision);
            return id is { } objectId
                ? await GitGrep.FileAsync(module, objectId, file, grep, options)
                : ([], $"Unknown revision {revision}");
        });

    private void WireDiffGitGrep()
        => _diffGitGrep = new GitGrepBox(this, DiffFilesGitGrepBox, DiffFilesGitGrepDeleteButton,
            new GitGrepBox.GitGrepButton(DiffFilesGitGrepButton, DiffFilesFindUsingMatchCaseMenuItem,
                DiffFilesFindUsingWholeWordMenuItem, DiffFilesFindUsingOptionsMenuItem, DiffFilesFindUsingDialogMenuItem,
                DiffFilesFindUsingInputBoxMenuItem, DiffFilesFindUsingBothMenuItem),
            _preferences, argument =>
            {
                _gitGrep = argument;
                ShowSelectionDiff();
            });
}
