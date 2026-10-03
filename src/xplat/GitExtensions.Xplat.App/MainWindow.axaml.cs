using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using GitCommands;
using GitCommands.Git;
using GitUIPluginInterfaces;
using System.Reactive;
using System.Reactive.Linq;

namespace GitExtensions.Xplat.App;

public partial class MainWindow : Window
{
    private const int MaxCommits = 500;

    // Developer aid: when set, the window is saved to this PNG once the initial repository has loaded, and the app exits.
    private const string ScreenshotEnvironmentVariable = "XPLAT_SCREENSHOT";

    public MainWindow()
    {
        InitializeComponent();
        OpenButton.Click += OnOpenClick;
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        string? initial = Program.InitialRepository;
        if (initial is null)
        {
            return;
        }

        PathBox.Text = initial;
        await LoadAsync(initial);

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
        await LoadAsync(PathBox.Text?.Trim() ?? "");
    }

    private async Task LoadAsync(string path)
    {
        OpenButton.IsEnabled = false;
        StatusText.Text = "Loading...";

        try
        {
            IReadOnlyList<CommitRow> rows = await Task.Run(() => LoadCommits(path));
            CommitList.ItemsSource = rows;
            StatusText.Text = $"{rows.Count} commits (up to {MaxCommits})";
        }
        catch (Exception ex)
        {
            CommitList.ItemsSource = null;
            StatusText.Text = ex.Message;
        }
        finally
        {
            OpenButton.IsEnabled = true;
        }
    }

    /// <summary>
    ///  Reads the history of <paramref name="path"/> with the shared git engine. Runs off the UI thread.
    /// </summary>
    private static IReadOnlyList<CommitRow> LoadCommits(string path)
    {
        GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), path);
        if (!module.IsValidGitWorkingDir())
        {
            throw new InvalidOperationException($"Not a git repository: {path}");
        }

        List<GitRevision> revisions = [];
        IObserver<IReadOnlyList<GitRevision>> observer =
            Observer.Create<IReadOnlyList<GitRevision>>(batch => revisions.AddRange(batch));

        new RevisionReader(module).GetLog(observer, revisionFilter: "HEAD", pathFilter: "", hasNotes: false,
            autostashLabel: "", cancellationToken: default);

        return revisions
            .Take(MaxCommits)
            .Select(r => new CommitRow(r.ObjectId.ToShortString(), r.Subject, r.Author ?? "",
                r.CommitDate.ToString("yyyy-MM-dd HH:mm")))
            .ToList();
    }
}

public sealed record CommitRow(string ShortHash, string Subject, string Author, string Date);
