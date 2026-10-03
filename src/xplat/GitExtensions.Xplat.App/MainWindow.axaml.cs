using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

public partial class MainWindow : Window
{
    // Developer aid: when set, the window is saved to this PNG once the initial repository has loaded, and the app exits.
    private const string ScreenshotEnvironmentVariable = "XPLAT_SCREENSHOT";
    private const string RemoteName = "origin";

    private readonly GitDiscoveryResult? _git;
    private readonly CommitListViewModel _commits = new(new GitCommitHistory());
    private readonly RepositoryViewModel _repository = new(new GitRepositoryService());
    private readonly RepositoryOperationsViewModel _actions = new(new GitOperations());

    public MainWindow(GitDiscoveryResult? git)
    {
        _git = git;
        InitializeComponent();
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://GitExtensions/Assets/git-extensions-logo-256px.png")));
        OpenButton.Click += OnOpenClick;
        LoadMoreButton.Click += OnLoadMoreClick;
        FetchButton.Click += (_, _) => RunOnRepository(path => _actions.FetchAsync(path, RemoteName));
        PullButton.Click += (_, _) =>
            RunOnCurrentBranch((path, branch) => _actions.PullAsync(path, RemoteName, branch, rebase: false));
        PushButton.Click += (_, _) =>
            RunOnCurrentBranch((path, branch) => _actions.PushAsync(path, RemoteName, branch));
        CloneButton.Click += OnCloneClick;
        StageButton.Click += (_, _) => StageSelected(staged: false);
        UnstageButton.Click += (_, _) => StageSelected(staged: true);
        CommitButton.Click += OnCommitClick;
        CreateBranchButton.Click += OnCreateBranchClick;
        CheckoutButton.Click +=
            (_, _) => RunOnSelectedBranch(branch => _actions.CheckoutAsync(branch.Path, branch.Name));
        DeleteBranchButton.Click += (_, _) =>
            RunOnSelectedBranch(branch => _actions.DeleteBranchAsync(branch.Path, branch.Name, force: false));
        CommitList.SelectionChanged += OnCommitSelectionChanged;
        _commits.PropertyChanged += (_, e) => OnCommitsChanged(e.PropertyName);
        _repository.PropertyChanged += (_, e) => OnRepositoryChanged(e.PropertyName);
        _actions.PropertyChanged += (_, e) => OnActionsChanged(e.PropertyName);
        _actions.RepositoryChanged += OnActionRepositoryChanged;
        Opened += OnOpened;
        ShowGitProblem();
        UpdateBusyState();
    }

    private void ShowGitProblem()
    {
        switch (_git?.Status)
        {
            case GitDiscoveryStatus.NotFound:
                GitProblemText.Text = OperatingSystem.IsMacOS()
                    ? "git was not found. Install the Command Line Tools with 'xcode-select --install', then restart."
                    : "git was not found. Install git, then restart.";
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
            PathBox.Text = repositoryPath;
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

    private async void OnCloneClick(object? sender, RoutedEventArgs e)
    {
        CloneRequest? request = await new CloneWindow().ShowDialog<CloneRequest?>(this);
        if (request is not null)
        {
            await _actions.CloneAsync(request.Url, request.TargetPath);
        }
    }

    private async void OnCommitClick(object? sender, RoutedEventArgs e)
    {
        if (_commits.RepositoryPath is not { } path)
        {
            return;
        }

        if (await _actions.CommitAsync(path, CommitMessageBox.Text ?? "", AmendCheck.IsChecked == true))
        {
            CommitMessageBox.Text = "";
        }
    }

    private async void OnCreateBranchClick(object? sender, RoutedEventArgs e)
    {
        if (_commits.RepositoryPath is not { } path)
        {
            return;
        }

        if (await _actions.CreateBranchAsync(path, NewBranchBox.Text ?? "", checkout: true))
        {
            NewBranchBox.Text = "";
        }
    }

    private async void StageSelected(bool staged)
    {
        if (_commits.RepositoryPath is not { } path)
        {
            return;
        }

        List<string> paths = ChangeList.SelectedItems?.OfType<FileChange>()
            .Where(change => change.Staged == staged)
            .Select(change => change.Path)
            .ToList() ?? [];
        if (paths.Count == 0)
        {
            return;
        }

        if (staged)
        {
            await _actions.UnstageAsync(path, paths);
        }
        else
        {
            await _actions.StageAsync(path, paths);
        }
    }

    private async void RunOnRepository(Func<string, Task<bool>> action)
    {
        if (_commits.RepositoryPath is { } path)
        {
            await action(path);
        }
    }

    private async void RunOnCurrentBranch(Func<string, string, Task<bool>> action)
    {
        if (_commits.RepositoryPath is { } path && _repository.CurrentBranch.Length > 0)
        {
            await action(path, _repository.CurrentBranch);
        }
    }

    private async void RunOnSelectedBranch(Func<(string Path, string Name), Task<bool>> action)
    {
        if (_commits.RepositoryPath is not { } path || BranchList.SelectedItem is not BranchInfo branch ||
            branch.IsRemote || branch.IsCurrent)
        {
            return;
        }

        await action((path, branch.Name));
    }

    private async void OnActionRepositoryChanged(object? sender, RepositoryChangedEventArgs e)
    {
        await OpenRepositoryAsync(e.RepositoryPath);
    }

    private void OnActionsChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(RepositoryOperationsViewModel.IsBusy):
                UpdateBusyState();
                break;
            case nameof(RepositoryOperationsViewModel.StatusMessage):
                OperationStatusText.Text = _actions.StatusMessage;
                break;
            case nameof(RepositoryOperationsViewModel.ErrorMessage):
                if (_actions.ErrorMessage is { } message)
                {
                    _actions.ClearError();
                    ShowError(message);
                }

                break;
        }
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
                UpdateBusyState();
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

    // Each control is enabled only when its action can run: nothing while a read or write is in progress, and
    // the repository actions only when a repository is open. Clone and Open need no repository, only git.
    private void UpdateBusyState()
    {
        bool busy = _commits.IsLoading || _repository.IsLoading || _actions.IsBusy;
        bool open = !busy && _commits.RepositoryPath is not null;
        bool gitAvailable = _git?.Status != GitDiscoveryStatus.NotFound;
        bool hasBranch = _repository.CurrentBranch.Length > 0;

        OpenButton.IsEnabled = !busy && gitAvailable;
        LoadMoreButton.IsEnabled = !busy;
        CloneButton.IsEnabled = !busy && gitAvailable;
        FetchButton.IsEnabled = open;
        PullButton.IsEnabled = open && hasBranch;
        PushButton.IsEnabled = open && hasBranch;
        StageButton.IsEnabled = open;
        UnstageButton.IsEnabled = open;
        CommitButton.IsEnabled = open;
        CreateBranchButton.IsEnabled = open;
        CheckoutButton.IsEnabled = open;
        DeleteBranchButton.IsEnabled = open;
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
