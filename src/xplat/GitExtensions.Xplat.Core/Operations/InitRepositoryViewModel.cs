using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormInit</c> (the Start menu's "Create new repository...", the command line's
///  <c>init</c> and the plugins' <c>StartInitializeDialog</c>): the folder, with the recent repositories in its list, a
///  personal repository or a central one (bare and shared), Browse and Create. Create checks the folder as upstream does,
///  makes the repository, shows git's answer and closes the window with true.
/// </summary>
public sealed partial class InitRepositoryViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormInit";

    /// <summary>
    ///  Upstream's <c>_initMsgBoxCaption</c>, also the window's title.
    /// </summary>
    public const string Caption = "Create new repository";

    /// <summary>
    ///  Upstream's <c>_chooseDirectory</c>.
    /// </summary>
    public const string ChooseDirectoryText = "Please choose a directory.";

    /// <summary>
    ///  Upstream's <c>_chooseDirectoryCaption</c>.
    /// </summary>
    public const string ChooseDirectoryCaption = "Choose directory";

    /// <summary>
    ///  Upstream's <c>_chooseDirectoryNotFile</c>.
    /// </summary>
    public const string ChooseDirectoryNotFileText = "Cannot initialize a new repository on a file.\nPlease choose a directory.";

    /// <summary>
    ///  Upstream's <c>TranslatedStrings.Error</c>.
    /// </summary>
    public const string ErrorCaption = "Error";

    private readonly Func<string, bool, Task<string>> _init;

    /// <param name="directory">The folder at first: upstream's default clone destination when empty.</param>
    /// <param name="recentDirectories">The recent repositories, which the folder's list offers.</param>
    /// <param name="init">Makes the repository in the folder (central: bare and shared) and returns git's answer.</param>
    public InitRepositoryViewModel(IDialogHost host, string directory, IReadOnlyList<string> recentDirectories,
        Func<string, bool, Task<string>> init)
        : base(host)
    {
        _init = init;
        RecentDirectories = recentDirectories;
        Directory = directory;
    }

    /// <summary>
    ///  The recent repositories, which the folder's list offers (upstream's repository history).
    /// </summary>
    public IReadOnlyList<string> RecentDirectories { get; }

    [ObservableProperty]
    public partial string Directory { get; set; }

    /// <summary>
    ///  Upstream's "Central repository, no working directory (--bare --shared=all)"; false is "Personal repository".
    /// </summary>
    [ObservableProperty]
    public partial bool Central { get; set; }

    /// <summary>
    ///  Upstream's "Personal repository", the other choice.
    /// </summary>
    public bool Personal
    {
        get => !Central;
        set => Central = !value;
    }

    /// <summary>
    ///  Chooses a folder (Browse); null when none was chosen. Set by the view.
    /// </summary>
    public Func<Task<string?>>? PickFolder { get; set; }

    /// <summary>
    ///  The repository Create made, once it did.
    /// </summary>
    public string? CreatedRepository { get; private set; }

    /// <summary>
    ///  Upstream's <c>IsRootedDirectoryPath</c>: a path that is not blank, is a valid directory path and is rooted.
    /// </summary>
    public static bool IsRootedDirectoryPath(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            // Throws for a path that is not valid, as upstream relies on.
            _ = new DirectoryInfo(path);
            return Path.IsPathRooted(path.Trim());
        }
        catch (Exception)
        {
            return false;
        }
    }

    partial void OnCentralChanged(bool value) => OnPropertyChanged(nameof(Personal));

    [RelayCommand]
    private Task BrowseAsync() => GuardAsync(async () =>
    {
        if (PickFolder is { } pick && await pick() is { } folder)
        {
            Directory = folder;
        }
    });

    // Upstream's InitClick.
    [RelayCommand]
    private Task CreateAsync() => GuardAsync(async () =>
    {
        string directory = Directory;
        if (!IsRootedDirectoryPath(directory))
        {
            await Host.ShowMessageAsync(Host.Translate(ChooseDirectoryText), Host.Translate(ChooseDirectoryCaption));
            return;
        }

        if (File.Exists(directory))
        {
            await Host.ShowMessageAsync(Host.Translate(ChooseDirectoryNotFileText), Host.Translate(ErrorCaption));
            return;
        }

        string answer = await _init(directory, Central);
        await Host.ShowMessageAsync(answer, Host.Translate(Caption));
        CreatedRepository = directory;
        Host.Close(true);
    });
}
