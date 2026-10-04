using Avalonia.Controls;
using Avalonia.Interactivity;
using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Lists the files of a commit and the history of the file selected in it.
/// </summary>
public partial class FileBrowserWindow : Window
{
    private readonly FileBrowserViewModel _viewModel = new(new GitCommitHistory());
    private readonly string _repositoryPath;
    private readonly string _hash;

    public FileBrowserWindow(string repositoryPath, string hash)
    {
        _repositoryPath = repositoryPath;
        _hash = hash;
        InitializeComponent();
        Title = $"Files at {hash[..Math.Min(8, hash.Length)]}";
        FileList.SelectionChanged += OnFileSelectionChanged;
        ShowHistoryDiffButton.Click += OnShowHistoryDiffClick;
        BlameButton.Click += OnBlameClick;
        _viewModel.PropertyChanged += (_, e) => OnViewModelChanged(e.PropertyName);
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        await _viewModel.OpenAsync(_repositoryPath, _hash);
    }

    private async void OnFileSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        await _viewModel.SelectFileAsync(FileList.SelectedItem as string);
    }

    private void OnBlameClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedFile is { } file)
        {
            new BlameWindow(_repositoryPath, _hash, file).Show(this);
        }
    }

    private void OnShowHistoryDiffClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedFile is { } file && HistoryList.SelectedItem is CommitRow row)
        {
            new DiffWindow(_repositoryPath, row.Hash, file, staged: false).Show(this);
        }
    }

    private void OnViewModelChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(FileBrowserViewModel.Files):
                FileList.ItemsSource = _viewModel.Files;
                break;
            case nameof(FileBrowserViewModel.SelectedFile):
                BlameButton.IsEnabled = _viewModel.SelectedFile is not null;
                break;
            case nameof(FileBrowserViewModel.FileHistory):
                HistoryList.ItemsSource = _viewModel.FileHistory;
                break;
            case nameof(FileBrowserViewModel.ErrorMessage):
                if (_viewModel.ErrorMessage is { } message)
                {
                    ErrorText.Text = message;
                    _viewModel.ClearError();
                }

                break;
        }
    }
}
