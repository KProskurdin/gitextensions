using Avalonia.Controls;
using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Shows, for each line of a file, the commit and author that last changed it, as of a commit.
/// </summary>
public partial class BlameWindow : Window
{
    private readonly GitCommitHistory _history = new();
    private readonly string _repositoryPath;
    private readonly string _hash;
    private readonly string _filePath;

    public BlameWindow(string repositoryPath, string hash, string filePath)
    {
        _repositoryPath = repositoryPath;
        _hash = hash;
        _filePath = filePath;
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.BlameWindow");
        Title = $"Blame: {filePath}";
        Opened += (_, _) => UiActions.Run(LoadAsync, ex => ErrorText.Text = ex.Message);
        BlameList.DoubleTapped += (_, _) => ShowSelectedLineCommit();
        BlameList.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;
                ShowSelectedLineCommit();
            }
        };
    }

    // Like upstream's blame, a line leads to the commit that last changed it: that commit's change to the file.
    // Lines not committed yet carry git's all-zero hash and have no commit to show.
    private void ShowSelectedLineCommit()
    {
        if (BlameList.SelectedItem is BlameLine line && line.Hash.Any(c => c != '0'))
        {
            new DiffWindow(_repositoryPath, line.Hash, _filePath, staged: false).Show(this);
        }
    }

    private async Task LoadAsync()
    {
        BlameList.ItemsSource = await _history.LoadBlameAsync(_repositoryPath, _hash, _filePath);
    }
}
