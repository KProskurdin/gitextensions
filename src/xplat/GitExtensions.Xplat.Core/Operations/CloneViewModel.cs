using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitCommands.Config;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormClone</c> (rules in <see cref="CloneDialog"/>): clones a URL or local repository
///  into a new subdirectory of the destination, with the branch to check out (read from the remote when the list opens),
///  the repository type and the options; afterwards it asks whether to open the clone, as upstream does when opened from
///  the browse window. Closes its window with true after cloning.
/// </summary>
public sealed partial class CloneViewModel : DialogViewModel
{
    /// <summary>
    ///  Upstream's BrowseForPrivateKey title.
    /// </summary>
    public const string SshKeyTitle = "Browse for key";

    private const string Origin = "origin";
    private const string ExceptionPrefix = "Exception: ";

    private readonly string? _url;
    private readonly GitModule _module;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private readonly IRemoteUrlHistory _remoteUrls;
    private readonly IReadOnlyList<string> _recentRepositories;
    private readonly Func<string, Task> _addRecent;
    private readonly Func<string, Task> _openRepository;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? _branchLoad;
    private string? _puttySshKey;

    /// <param name="module">The open repository, whose remote and folder the dialog proposes.</param>
    /// <param name="actions">Runs the clone.</param>
    /// <param name="preferences">Upstream's default clone destination.</param>
    /// <param name="remoteUrls">The remote URL history the source box lists and the clone's source is added to.</param>
    /// <param name="recentRepositories">The recent local repositories, whose parent folders are offered as
    ///  destinations.</param>
    /// <param name="addRecent">Adds the clone to the recent repositories.</param>
    /// <param name="openRepository">Opens the clone, when the user wants to.</param>
    /// <param name="url">The URL (or destination folder) to start with; null for none.</param>
    public CloneViewModel(IDialogHost host, GitModule module, RepositoryOperationsViewModel actions,
        IAppPreferences preferences, IRemoteUrlHistory remoteUrls, IReadOnlyList<string> recentRepositories,
        Func<string, Task> addRecent, Func<string, Task> openRepository, string? url)
        : base(host)
    {
        _module = module;
        _actions = actions;
        _preferences = preferences;
        _remoteUrls = remoteUrls;
        _recentRepositories = recentRepositories;
        _addRecent = addRecent;
        _openRepository = openRepository;
        _url = url;
        ShowLocationInfo();
    }

    /// <summary>
    ///  Raised when the destination needs correcting: the window focuses its box.
    /// </summary>
    public event EventHandler? DestinationRequired;

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public CloneDialog Dialog { get; } = new();

    /// <summary>
    ///  The remote URL history the source box lists.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> FromHistory { get; private set; } = [];

    /// <summary>
    ///  The recent repositories' folders the destination box lists.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Destinations { get; private set; } = [];

    [ObservableProperty]
    public partial string? From { get; set; }

    [ObservableProperty]
    public partial string? Destination { get; set; }

    [ObservableProperty]
    public partial string? Subdirectory { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> BranchItems { get; private set; } = CloneDialog.DefaultBranchItems;

    [ObservableProperty]
    public partial string? Branch { get; set; } = CloneDialog.BranchDefaultRemoteHead;

    /// <summary>
    ///  Upstream's "Public repository, no working directory (--bare)".
    /// </summary>
    [ObservableProperty]
    public partial bool Bare { get; set; }

    [ObservableProperty]
    public partial bool InitializeAllSubmodules { get; set; } = true;

    [ObservableProperty]
    public partial bool DownloadFullHistory { get; set; } = true;

    /// <summary>
    ///  Upstream's Info label: where the clone goes.
    /// </summary>
    [ObservableProperty]
    public partial string LocationInfo { get; private set; } = "";

    /// <summary>
    ///  The location is a problem: the label shows in red.
    /// </summary>
    [ObservableProperty]
    public partial bool LocationIsError { get; private set; }

    /// <summary>
    ///  Completes when the histories, the source and the destination are shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  The last read of the remote's branches, for tests.
    /// </summary>
    public Task BranchesLoaded { get; private set; } = Task.CompletedTask;

    /// <summary>
    ///  Reads the clipboard's text, or null when it cannot be read; the window sets it, as the clipboard is the
    ///  platform's.
    /// </summary>
    public Func<Task<string?>>? ReadClipboard { get; set; }

    /// <summary>
    ///  Picks a folder starting at the given one; the window sets it, as the picker is the platform's.
    /// </summary>
    public Func<string?, Task<string?>>? PickFolder { get; set; }

    /// <summary>
    ///  Picks a PuTTY key file; the window sets it.
    /// </summary>
    public Func<Task<string?>>? PickSshKey { get; set; }

    /// <summary>
    ///  Gives a key to Pageant, returning the error if any. Tests replace it, so that none starts the user's Pageant.
    /// </summary>
    public Func<string, string?> GiveKeyToPageant { get; set; } = _ => null;

    /// <summary>
    ///  Upstream's <c>OnRuntimeLoad</c>: the remote history, the recent repositories' folders, and the source and
    ///  destination proposed from the URL given, the clipboard or the open repository. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        try
        {
            IList<GitCommands.UserRepositoryHistory.Repository> remotes = await _remoteUrls.LoadAsync();
            FromHistory = [.. remotes.Select(remote => remote.Path)];
            Destinations = await Task.Run(() => CloneDialog.DestinationsFromRecent(_recentRepositories));

            string? clipboardText = PathUtil.CanBeGitURL(_url) || ReadClipboard is null ? null : await ReadClipboard();
            string defaultDestination = _preferences.DefaultCloneDestinationPath;
            (string from, string destination) = await Task.Run(() =>
                CloneDialog.Initial(_url, defaultDestination, clipboardText, _module));
            Destination = destination;
            From = from;
            OnFromChanged(from);
        }
        finally
        {
            _loaded.TrySetResult();
        }
    });

    /// <summary>
    ///  Upstream's <c>LoadBranches</c>, when the branch list opens: the remote's branches after the defaults, keeping the
    ///  text when it is one of them.
    /// </summary>
    public void LoadBranches() => BranchesLoaded = GuardAsync(LoadBranchesAsync);

    /// <summary>
    ///  Stops reading the remote's branches; the window calls it when it closes.
    /// </summary>
    public void Cancel() => _branchLoad?.Cancel();

    /// <summary>
    ///  Upstream's <c>BrowseAndLoad</c>: the key is given to Pageant, and used only when that worked.
    /// </summary>
    public async Task UseSshKeyAsync(string path)
    {
        string? error = await Task.Run(() => GiveKeyToPageant(path));
        if (error is null)
        {
            _puttySshKey = path;
            return;
        }

        await Host.ShowMessageAsync(error, SshKeyTitle);
    }

    // Upstream's FromTextUpdate: the subdirectory named after the repository, and the branch list back to its defaults.
    partial void OnFromChanged(string? value)
    {
        Dialog.From = value ?? "";
        Subdirectory = Dialog.Subdirectory;
        _branchLoad?.Cancel();
        BranchItems = CloneDialog.DefaultBranchItems;
        Branch = CloneDialog.BranchDefaultRemoteHead;
        ShowLocationInfo();
    }

    partial void OnDestinationChanged(string? value) => ShowLocationInfo();

    partial void OnSubdirectoryChanged(string? value) => ShowLocationInfo();

    // Upstream's ToTextUpdate.
    private void ShowLocationInfo()
    {
        Dialog.Destination = Destination ?? "";
        Dialog.Subdirectory = Subdirectory ?? "";
        (LocationInfo, LocationIsError) = Dialog.LocationInfo(Host.Translate);
    }

    private async Task LoadBranchesAsync()
    {
        if (_branchLoad is { } previous)
        {
            await previous.CancelAsync();
        }

        CancellationTokenSource load = new();
        _branchLoad = load;
        CancellationToken cancellationToken = load.Token;
        string from = From ?? "";
        (IReadOnlyList<IGitRef> refs, string? error) = await Task.Run(() =>
        {
            IReadOnlyList<IGitRef> found = _module.GetRemoteServerRefs(from, false, true, out string? errorOutput,
                cancellationToken);
            return (found, errorOutput);
        });
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException(error);
        }

        string text = Branch ?? "";
        IReadOnlyList<string> names = CloneDialog.BranchItems(refs);
        BranchItems = names;
        Branch = names.Contains(text) ? text : CloneDialog.BranchDefaultRemoteHead;
    }

    [RelayCommand]
    private Task BrowseFromAsync() => GuardAsync(async () =>
    {
        if (PickFolder is not null && await PickFolder(From) is { } path)
        {
            From = path;
        }
    });

    [RelayCommand]
    private Task BrowseDestinationAsync() => GuardAsync(async () =>
    {
        if (PickFolder is not null && await PickFolder(Destination) is { } path)
        {
            Destination = path;
        }
    });

    // Upstream's BrowseForPrivateKey: the key is set on the clone's origin after cloning.
    [RelayCommand]
    private Task LoadSshKeyAsync() => GuardAsync(async () =>
    {
        if (PickSshKey is not null && await PickSshKey() is { } path)
        {
            await UseSshKeyAsync(path);
        }
    });

    // Upstream's OkClick: the checks, the new folder, the clone with git's progress, the histories, the SSH key and the
    // question whether to open the clone.
    [RelayCommand]
    private Task CloneAsync() => GuardAsync(async () =>
    {
        if (_branchLoad is { } load)
        {
            await load.CancelAsync();
        }

        Dialog.From = From ?? "";
        Dialog.Destination = Destination ?? "";
        Dialog.Subdirectory = Subdirectory ?? "";
        Dialog.Branch = Branch ?? "";
        Dialog.Bare = Bare;
        Dialog.InitializeAllSubmodules = InitializeAllSubmodules;
        Dialog.DownloadFullHistory = DownloadFullHistory;
        string caption = Host.Translate(CloneDialog.ErrorCloneFailed);
        if (Dialog.Validate() is { } error)
        {
            await Host.ShowMessageAsync(Host.Translate(error), caption);
            DestinationRequired?.Invoke(this, EventArgs.Empty);
            return;
        }

        string target;
        ArgumentString arguments;
        try
        {
            target = Dialog.TargetPath();
            arguments = await Task.Run(() =>
            {
                Directory.CreateDirectory(target);
                return Dialog.Arguments(target, GitModules.Open(target).GetPathForGitExecution);
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                       or NotSupportedException)
        {
            await Host.ShowMessageAsync(ExceptionPrefix + ex.Message, caption);
            return;
        }

        if (!await _actions.CloneAsync(Dialog.From, target, arguments))
        {
            return;
        }

        await _remoteUrls.AddAsMostRecentAsync(Dialog.From.Trim());
        await _addRecent(target);
        if (_puttySshKey is { Length: > 0 } key)
        {
            await Task.Run(() => GitModules.Open(target)
                .SetSetting(string.Format(SettingKeyString.RemotePuttySshKey, Origin), key));
        }

        if (await Host.ConfirmAsync(string.Format(Host.Translate(CloneDialog.QuestionOpenRepo), target),
                Host.Translate(CloneDialog.QuestionOpenRepoCaption)))
        {
            await _openRepository(target);
        }

        Host.Close(true);
    });
}
