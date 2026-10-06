using Avalonia.Controls;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Edits the repository's .gitignore, optionally with new patterns already added: the new shell's version of upstream
///  <c>FormGitIgnore</c> and <c>FormAddToGitIgnore</c>. Closes with true when the file was saved.
/// </summary>
public partial class GitIgnoreWindow : Window
{
    private readonly string _repositoryPath;
    private readonly IReadOnlyList<string> _patternsToAdd;

    public GitIgnoreWindow(string repositoryPath, IReadOnlyList<string>? patternsToAdd = null)
    {
        _repositoryPath = repositoryPath;
        _patternsToAdd = patternsToAdd ?? [];
        InitializeComponent();
        Title = _patternsToAdd.Count > 0 ? "Add to .gitignore" : "Edit .gitignore";
        SaveButton.IsEnabled = false;
        CancelButton.Click += (_, _) => Close(false);
        SaveButton.Click += (_, _) => UiActions.Run(SaveAsync, ex => ErrorText.Text = ex.Message);
        Opened += (_, _) => UiActions.Run(LoadAsync, ex => ErrorText.Text = ex.Message);
    }

    private async Task LoadAsync()
    {
        string content = await GitIgnoreFile.ReadAsync(_repositoryPath);
        ContentBox.Text = GitIgnoreFile.AddPatterns(content, _patternsToAdd);
        SaveButton.IsEnabled = true;
    }

    private async Task SaveAsync()
    {
        await GitIgnoreFile.WriteAsync(_repositoryPath, ContentBox.Text ?? "");
        Close(true);
    }
}
