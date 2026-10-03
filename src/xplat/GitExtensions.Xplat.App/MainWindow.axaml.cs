using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

public partial class MainWindow : Window
{
    // Developer aid: when set, the window is saved to this PNG once the initial repository has loaded, and the app exits.
    private const string ScreenshotEnvironmentVariable = "XPLAT_SCREENSHOT";

    private readonly GitDiscoveryResult? _git;
    private readonly CommitListViewModel _commits = new(new GitCommitHistory());
    private readonly RepositoryViewModel _repository = new(new GitRepositoryService());

    public MainWindow(GitDiscoveryResult? git)
    {
        _git = git;
        InitializeComponent();
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://GitExtensions/Assets/git-extensions-logo-256px.png")));
        OpenButton.Click += OnOpenClick;
        LoadMoreButton.Click += OnLoadMoreClick;
        CommitList.SelectionChanged += OnCommitSelectionChanged;
        _commits.PropertyChanged += (_, e) => OnCommitsChanged(e.PropertyName);
        _repository.PropertyChanged += (_, e) => OnRepositoryChanged(e.PropertyName);
        Opened += OnOpened;
        ShowGitProblem();
    }

    private void ShowGitProblem()
    {
        switch (_git?.Status)
        {
            case GitDiscoveryStatus.NotFound:
                GitProblemText.Text = OperatingSystem.IsMacOS()
                    ? "git was not found. Install the Command Line Tools with 'xcode-select --install', then restart."
                    : "git was not found. Install git, then restart.";
                OpenButton.IsEnabled = false;
                break;
            case GitDiscoveryStatus.TooOld:
                GitProblemText.Text =
                    $"git {_git.Version} is older than the supported minimum. Install a newer git, then restart.";
                break;
            case GitDiscoveryStatus.Found or null:
                break;
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        string? initial = Program.InitialRepository;
        if (initial is null || _git?.Status == GitDiscoveryStatus.NotFound)
        {
            return;
        }

        PathBox.Text = initial;
        await OpenRepositoryAsync(initial);

        string? screenshot = Environment.GetEnvironmentVariable(ScreenshotEnvironmentVariable);
        if (!string.IsNullOrEmpty(screenshot))
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            using RenderTargetBitmap bitmap = new(new PixelSize((int)Bounds.Width, (int)Bounds.Height));
            bitmap.Render(this);
            bitmap.Save(screenshot);
            Close();
        }
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        await OpenRepositoryAsync(PathBox.Text?.Trim() ?? "");
    }

    private async Task OpenRepositoryAsync(string path)
    {
        await _commits.OpenAsync(path);
        if (_commits.RepositoryPath is { } repositoryPath)
        {
            await _repository.RefreshAsync(repositoryPath);
        }
    }

    private async void OnLoadMoreClick(object? sender, RoutedEventArgs e)
    {
        await _commits.LoadMoreAsync();
    }

    private async void OnCommitSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        await _commits.SelectAsync(CommitList.SelectedItem as CommitRow);
    }

    private void OnCommitsChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(CommitListViewModel.Rows):
                CommitList.ItemsSource = _commits.Rows;
                break;
            case nameof(CommitListViewModel.Status):
                StatusText.Text = _commits.Status;
                break;
            case nameof(CommitListViewModel.RepositoryName):
                Title = _commits.RepositoryName.Length == 0
                    ? "Git Extensions"
                    : $"{_commits.RepositoryName} - Git Extensions";
                break;
            case nameof(CommitListViewModel.HasMore):
                LoadMoreButton.IsVisible = _commits.HasMore;
                break;
            case nameof(CommitListViewModel.IsLoading):
                UpdateBusyState();
                break;
            case nameof(CommitListViewModel.Details):
            case nameof(CommitListViewModel.DetailsError):
                ShowDetails();
                break;
            case nameof(CommitListViewModel.ErrorMessage):
                if (_commits.ErrorMessage is { } message)
                {
                    _commits.ClearError();
                    ShowError(message);
                }

                break;
        }
    }

    private void OnRepositoryChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(RepositoryViewModel.CurrentBranch):
                BranchText.Text = _repository.CurrentBranch.Length == 0 ? "" : $"Branch: {_repository.CurrentBranch}";
                break;
            case nameof(RepositoryViewModel.Branches):
                BranchList.ItemsSource = _repository.Branches;
                break;
            case nameof(RepositoryViewModel.Changes):
                ChangeList.ItemsSource = _repository.Changes;
                break;
            case nameof(RepositoryViewModel.IsLoading):
                UpdateBusyState();
                break;
            case nameof(RepositoryViewModel.ErrorMessage):
                if (_repository.ErrorMessage is { } message)
                {
                    _repository.ClearError();
                    ShowError(message);
                }

                break;
        }
    }

    // Open and Load more stay disabled while either the commit list or the repository panel is reading.
    private void UpdateBusyState()
    {
        bool busy = _commits.IsLoading || _repository.IsLoading;
        OpenButton.IsEnabled = !busy && _git?.Status != GitDiscoveryStatus.NotFound;
        LoadMoreButton.IsEnabled = !busy;
    }

    private void ShowDetails()
    {
        CommitDetails? details = _commits.Details;
        if (details is null)
        {
            DetailHash.Text = "";
            DetailAuthor.Text = "";
            DetailAuthorDate.Text = "";
            DetailCommitDate.Text = "";
            DetailParents.Text = "";
            DetailMessage.Text = _commits.DetailsError;
            return;
        }

        DetailHash.Text = details.Hash;
        DetailAuthor.Text = details.Author;
        DetailAuthorDate.Text = $"Authored: {details.AuthorDate}";
        DetailCommitDate.Text = $"Committed: {details.CommitDate}";
        DetailParents.Text = string.IsNullOrEmpty(details.Parents) ? "No parents" : $"Parents: {details.Parents}";
        DetailMessage.Text = details.Message;
    }

    private void ShowError(string message)
    {
        _ = new ErrorWindow(message).ShowDialog(this);
    }
}
