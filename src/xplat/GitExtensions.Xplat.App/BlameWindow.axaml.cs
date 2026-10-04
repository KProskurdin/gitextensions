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
        Title = $"Blame: {filePath}";
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            BlameList.ItemsSource = await _history.LoadBlameAsync(_repositoryPath, _hash, _filePath);
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
    }
}
