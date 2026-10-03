using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using GitCommands;
using GitCommands.Git;

namespace GitExtensions.Xplat.App;

public partial class MainWindow : Window
{
    // Developer aid: when set, the window is saved to this PNG once the initial repository has loaded, and the app exits.
    private const string ScreenshotEnvironmentVariable = "XPLAT_SCREENSHOT";

    private readonly GitDiscoveryResult? _git;
    private string? _repoPath;
    private int _pages;
    private int _detailsRequest;

    public MainWindow(GitDiscoveryResult? git)
    {
        _git = git;
        InitializeComponent();
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://GitExtensions/Assets/git-extensions-logo-256px.png")));
        OpenButton.Click += OnOpenClick;
        LoadMoreButton.Click += OnLoadMoreClick;
        CommitList.SelectionChanged += OnCommitSelectionChanged;
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

    private async void OnLoadMoreClick(object? sender, RoutedEventArgs e)
    {
        if (_repoPath is null)
        {
            return;
        }

        await LoadPagesAsync(_repoPath, _pages + 1);
    }

    private Task OpenRepositoryAsync(string path) => LoadPagesAsync(path, pages: 1);

    private async Task LoadPagesAsync(string path, int pages)
    {
        OpenButton.IsEnabled = false;
        LoadMoreButton.IsEnabled = false;
        StatusText.Text = "Loading...";
        ClearDetails();

        try
        {
            CommitPage page = await Task.Run(() => CommitHistory.LoadPage(path, pages * CommitHistory.PageSize));
            _repoPath = path;
            _pages = pages;
            Title = $"{Path.GetFileName(path.TrimEnd('/', '\\'))} - Git Extensions";
            CommitList.ItemsSource = page.Rows;
            LoadMoreButton.IsVisible = page.HasMore;
            StatusText.Text = page.HasMore
                ? $"{page.Rows.Count} commits, more available"
                : $"{page.Rows.Count} commits";
        }
        catch (Exception ex)
        {
            _repoPath = null;
            _pages = 0;
            CommitList.ItemsSource = null;
            LoadMoreButton.IsVisible = false;
            StatusText.Text = "";
            await new ErrorWindow(ex.Message).ShowDialog(this);
        }
        finally
        {
            OpenButton.IsEnabled = true;
            LoadMoreButton.IsEnabled = true;
        }
    }

    private async void OnCommitSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CommitList.SelectedItem is not CommitRow row || _repoPath is null)
        {
            ClearDetails();
            return;
        }

        int request = ++_detailsRequest;
        string repoPath = _repoPath;

        try
        {
            CommitDetails details = await Task.Run(() => CommitHistory.LoadDetails(repoPath, row.Hash));
            if (request == _detailsRequest)
            {
                ShowDetails(details);
            }
        }
        catch (Exception ex)
        {
            if (request == _detailsRequest)
            {
                ClearDetails();
                DetailMessage.Text = ex.Message;
            }
        }
    }

    private void ShowDetails(CommitDetails details)
    {
        DetailHash.Text = details.Hash;
        DetailAuthor.Text = details.Author;
        DetailAuthorDate.Text = $"Authored: {details.AuthorDate}";
        DetailCommitDate.Text = $"Committed: {details.CommitDate}";
        DetailParents.Text = string.IsNullOrEmpty(details.Parents) ? "No parents" : $"Parents: {details.Parents}";
        DetailMessage.Text = details.Message;
    }

    private void ClearDetails()
    {
        _detailsRequest++;
        DetailHash.Text = "";
        DetailAuthor.Text = "";
        DetailAuthorDate.Text = "";
        DetailCommitDate.Text = "";
        DetailParents.Text = "";
        DetailMessage.Text = "";
    }
}
