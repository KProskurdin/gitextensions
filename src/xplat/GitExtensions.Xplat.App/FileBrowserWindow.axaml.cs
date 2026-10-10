using Avalonia.Controls;
using Avalonia.Interactivity;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Lists the files of a commit and the history of the file selected in it.
/// </summary>
public partial class FileBrowserWindow : Window
{
    private readonly FileBrowserViewModel _viewModel = new(new GitCommitHistory(
        () => new CommitDateStyle(AppServices.Preferences.RelativeDate, AppServices.Preferences.ShowAuthorDate),
        () => AppServices.Preferences.RevisionSortOrder, fileHistory: () => AppServices.Preferences.FileHistory));

    private readonly string _repositoryPath;
    private readonly string _hash;
    private readonly Func<IRepositoryHostPlugin?>? _repositoryHost;

    /// <param name="repositoryHost">The repository host plugin of the repository, for the blame menu (e.g. "View in GitHub").</param>
    /// <param name="selectedFile">A file to select, whose history then shows (upstream's file history of a file).</param>
    public FileBrowserWindow(string repositoryPath, string hash, Func<IRepositoryHostPlugin?>? repositoryHost = null,
        string? selectedFile = null)
    {
        _repositoryHost = repositoryHost;
        _repositoryPath = repositoryPath;
        _hash = hash;
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.FileBrowserWindow");
        Title = $"Files at {hash[..Math.Min(8, hash.Length)]}";
        FileList.SelectionChanged += (_, _) => Run(() => _viewModel.SelectFileAsync(FileList.SelectedItem as string));
        ShowHistoryDiffButton.Click += OnShowHistoryDiffClick;
        BlameButton.Click += OnBlameClick;
        FileHistoryOptions options = AppServices.Preferences.FileHistory;
        FollowRenamesCheck.IsChecked = options.FollowRenames;
        ExactRenamesCheck.IsChecked = options.ExactRenamesOnly;
        ExactRenamesCheck.IsEnabled = options.FollowRenames;
        FollowRenamesCheck.IsCheckedChanged += (_, _) => ChangeFileHistoryOptions();
        ExactRenamesCheck.IsCheckedChanged += (_, _) => ChangeFileHistoryOptions();
        _viewModel.PropertyChanged += (_, e) => OnViewModelChanged(e.PropertyName);
        Opened += (_, _) => Run(async () =>
        {
            await _viewModel.OpenAsync(_repositoryPath, _hash);
            if (selectedFile is not null)
            {
                FileList.SelectedItem = selectedFile;
            }
        });
    }

    private void Run(Func<Task> action) => UiActions.Run(action, ex => ErrorText.Text = ex.Message);

    // As upstream's menu items: the setting changes for every file history, and this one is read again.
    private void ChangeFileHistoryOptions()
    {
        AppServices.Preferences.FileHistory = new FileHistoryOptions(FollowRenamesCheck.IsChecked == true,
            ExactRenamesCheck.IsChecked == true);
        ExactRenamesCheck.IsEnabled = FollowRenamesCheck.IsChecked == true;
        Run(_viewModel.ReloadHistoryAsync);
    }

    private void OnBlameClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedFile is { } file)
        {
            new BlameWindow(_repositoryPath, _hash, file, _repositoryHost).Show(this);
        }
    }

    private void OnShowHistoryDiffClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedFile is { } file && HistoryList.SelectedItem is CommitRow row)
        {
            // The file's name in that commit, which may be an earlier one when the history follows renames.
            new DiffWindow(_repositoryPath, row.Hash, _viewModel.PathIn(row) ?? file, staged: false).Show(this);
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
