using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The view model of upstream's <c>FormOpenDirectory</c> (the Start menu's and the dashboard's "Open repository"): a
///  folder typed or chosen from upstream's list, Go up, Browse and Open. Open checks the folder is a git working
///  directory, as upstream's <c>OpenGitRepository</c>, and closes the window with true.
/// </summary>
public sealed partial class OpenDirectoryViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormOpenDirectory";

    /// <summary>
    ///  Upstream's <c>_warningOpenFailed</c>.
    /// </summary>
    public const string WarningOpenFailed = "The selected directory is not a valid git repository.";

    /// <summary>
    ///  Upstream's <c>TranslatedStrings.Error</c>.
    /// </summary>
    public const string ErrorCaption = "Error";

    private readonly Func<string, bool> _isRepository;

    /// <param name="directories">The list, from <see cref="ListDirectories"/>; the folder shows its first entry.</param>
    /// <param name="isRepository">Whether a folder is a git working directory (upstream's <c>IsValidGitWorkingDir</c>).</param>
    public OpenDirectoryViewModel(IDialogHost host, IReadOnlyList<string> directories, Func<string, bool> isRepository)
        : base(host)
    {
        _isRepository = isRepository;
        Directories = directories;
        Directory = directories.FirstOrDefault() ?? "";
    }

    public IReadOnlyList<string> Directories { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoUp))]
    [NotifyCanExecuteChangedFor(nameof(GoUpCommand))]
    public partial string Directory { get; set; }

    /// <summary>
    ///  Upstream's <c>_NO_TRANSLATE_Directory_TextChanged</c>: Go up is enabled for an existing folder that has a parent.
    /// </summary>
    public bool CanGoUp => ParentOf(Directory) is not null;

    /// <summary>
    ///  Chooses a folder (Browse), starting at the one given; null when none was chosen. Set by the view.
    /// </summary>
    public Func<string, Task<string?>>? PickFolder { get; set; }

    /// <summary>
    ///  The repository Open chose, once it did; without a trailing separator, as the app keeps repository paths.
    /// </summary>
    public string? ChosenRepository { get; private set; }

    /// <summary>
    ///  Upstream's <c>GetDirectories</c>: the default clone folder, the open repository's parent folder and the recent
    ///  repositories; with none of those, the repository opened last and the home folder. Folders end with a separator,
    ///  as upstream's list shows them, and each is listed once.
    /// </summary>
    public static IReadOnlyList<string> ListDirectories(string? defaultCloneDestination, string? openRepository,
        IEnumerable<string> recentRepositories, string? recentWorkingDir, string? homeDir)
    {
        List<string> directories = [];
        if (!string.IsNullOrWhiteSpace(defaultCloneDestination))
        {
            directories.Add(WithSeparator(defaultCloneDestination));
        }

        if (!string.IsNullOrWhiteSpace(openRepository) &&
            new DirectoryInfo(openRepository).Parent is { } parent)
        {
            directories.Add(WithSeparator(parent.FullName));
        }

        directories.AddRange(recentRepositories);
        if (directories.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(recentWorkingDir))
            {
                directories.Add(WithSeparator(recentWorkingDir));
            }

            if (!string.IsNullOrWhiteSpace(homeDir))
            {
                directories.Add(WithSeparator(homeDir));
            }
        }

        return [.. directories.Distinct()];
    }

    // Upstream's folderGoUpButton_Click: the parent folder, then a separator typed after it so the list's completion
    // offers its folders.
    [RelayCommand(CanExecute = nameof(CanGoUp))]
    private void GoUp()
    {
        if (ParentOf(Directory) is { } parent)
        {
            Directory = WithSeparator(parent);
        }
    }

    // Upstream's folderBrowserButton_Click: a chosen folder is opened at once.
    [RelayCommand]
    private Task BrowseAsync() => GuardAsync(async () =>
    {
        if (PickFolder is { } pick && await pick(Directory) is { Length: > 0 } folder)
        {
            Directory = folder;
            await OpenCoreAsync();
        }
    });

    // Upstream's LoadClick.
    [RelayCommand]
    private Task OpenAsync() => GuardAsync(OpenCoreAsync);

    private async Task OpenCoreAsync()
    {
        Directory = Directory.Trim();
        string directory = Directory;
        if (System.IO.Directory.Exists(directory) && _isRepository(directory))
        {
            ChosenRepository = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            Host.Close(true);
            return;
        }

        await Host.ShowMessageAsync(Host.Translate(WarningOpenFailed), Host.Translate(ErrorCaption));
    }

    private static string? ParentOf(string directory)
    {
        try
        {
            DirectoryInfo folder = new(directory);
            return folder.Exists ? folder.Parent?.FullName : null;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException
                                       or NotSupportedException)
        {
            // Upstream's catch: what is typed is no folder path yet.
            return null;
        }
    }

    // Upstream's EnsureTrailingPathSeparator.
    private static string WithSeparator(string path)
        => Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
}
