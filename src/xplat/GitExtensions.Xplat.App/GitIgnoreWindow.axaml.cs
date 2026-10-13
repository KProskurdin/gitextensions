using Avalonia.Controls;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Edits the repository's .gitignore or its local excludes (.git/info/exclude), optionally with new patterns already
///  added: the new shell's version of upstream <c>FormGitIgnore</c> and <c>FormAddToGitIgnore</c>. Closes with true when the
///  file was saved.
/// </summary>
public partial class GitIgnoreWindow : Window
{
    private readonly string _repositoryPath;
    private readonly IReadOnlyList<string> _patternsToAdd;
    private readonly bool _localExclude;

    public GitIgnoreWindow(string repositoryPath, IReadOnlyList<string>? patternsToAdd = null, bool localExclude = false)
    {
        _repositoryPath = repositoryPath;
        _patternsToAdd = patternsToAdd ?? [];
        _localExclude = localExclude;
        InitializeComponent();

        // Upstream's GitIgnoreModel, GitLocalExcludeModel and FormAddToGitIgnore captions.
        Title = (_patternsToAdd.Count > 0, localExclude) switch
        {
            (true, true) => "Add file(s) to .git/info/exclude",
            (true, false) => "Add to .gitignore",
            (false, true) => "Edit .git/info/exclude",
            _ => "Edit .gitignore",
        };
        SaveButton.IsEnabled = false;
        CancelButton.Click += (_, _) => Close(false);
        SaveButton.Click += (_, _) => UiActions.Run(SaveAsync, ex => ErrorText.Text = ex.Message);
        Opened += (_, _) => UiActions.Run(LoadAsync, ex => ErrorText.Text = ex.Message);
    }

    private async Task LoadAsync()
    {
        string content = await GitIgnoreFile.ReadAsync(_repositoryPath, _localExclude);
        ContentBox.Text = GitIgnoreFile.AddPatterns(content, _patternsToAdd);
        SaveButton.IsEnabled = true;
    }

    private async Task SaveAsync()
    {
        await GitIgnoreFile.WriteAsync(_repositoryPath, ContentBox.Text ?? "", _localExclude);
        Close(true);
    }
}
