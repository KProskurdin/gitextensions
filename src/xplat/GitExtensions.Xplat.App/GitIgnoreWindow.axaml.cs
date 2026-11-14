using Avalonia.Controls;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormGitIgnore</c>, bound to <see cref="GitIgnoreViewModel"/>: edits the repository's .gitignore or its
///  local excludes (.git/info/exclude), with upstream's default ignores, "Add pattern" (<see cref="AddToGitIgnoreWindow"/>),
///  help text and links, and its question when the window closes with unsaved changes. <see cref="Saved"/> tells whether
///  the file was written.
/// </summary>
public partial class GitIgnoreWindow : Window
{
    public GitIgnoreWindow()
        : this("", localExclude: false)
    {
    }

    public GitIgnoreWindow(string repositoryPath, bool localExclude = false)
    {
        ViewModel = new GitIgnoreViewModel(new WindowDialogHost(this),
            repositoryPath.Length > 0 ? GitIgnoreFile.PathIn(repositoryPath, localExclude) : "", localExclude,
            () => GitIgnoreDialog.IsBare(repositoryPath),
            () => AddToGitIgnoreWindow.ShowAsync(this, repositoryPath, localExclude,
                [GitIgnoreDialog.ProposedPattern]));
        InitializeComponent();
        UpstreamTranslation.Apply(this, GitIgnoreViewModel.Category);

        // After the translation pass, which would give the local excludes the .gitignore title: the view model translates
        // the title and the help.
        DataContext = ViewModel;
        InitialFocus.Attach(this, () => _NO_TRANSLATE_GitIgnoreEdit);

        // View concerns: the links open the browser, the editor takes the focus once the file is read, and a close with
        // unsaved changes waits for upstream's question, which closes the window again once answered.
        lnkGitIgnorePatterns.Click += (_, _) => OsShellUtil.OpenUrlInDefaultBrowser(GitIgnoreDialog.PatternsUrl);
        lnkGitIgnoreGenerate.Click += (_, _) => OsShellUtil.OpenUrlInDefaultBrowser(GitIgnoreDialog.GenerateUrl);
        Closing += (sender, e) =>
        {
            if (!ViewModel.MayClose)
            {
                e.Cancel = true;
                _ = ViewModel.AskToCloseAsync();
            }
        };
        Opened += (_, _) => _ = ShowFileAsync();
    }

    public GitIgnoreViewModel ViewModel { get; }

    /// <summary>
    ///  True when the file was written while the window was open (by Save, the closing question or "Add pattern").
    /// </summary>
    public bool Saved => ViewModel.Saved;

    /// <summary>
    ///  Completes when the file has been read into the editor.
    /// </summary>
    public Task FileLoaded => ViewModel.Loaded;

    /// <summary>
    ///  Upstream's <c>StartEditGitIgnoreDialog</c>: the window shown over <paramref name="owner"/>; true when the file was
    ///  saved.
    /// </summary>
    public static async Task<bool> ShowAsync(Window owner, string repositoryPath, bool localExclude)
    {
        GitIgnoreWindow window = new(repositoryPath, localExclude);
        await window.ShowDialog(owner);
        return window.Saved;
    }

    private async Task ShowFileAsync()
    {
        await ViewModel.LoadAsync();
        _NO_TRANSLATE_GitIgnoreEdit.Focus();
    }
}
