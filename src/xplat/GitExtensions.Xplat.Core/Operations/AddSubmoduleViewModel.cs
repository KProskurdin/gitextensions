using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormAddSubmodule</c> (rules in <see cref="SubmodulesDialog"/>): <c>git submodule add</c>
///  of a repository at a local path, on a branch, forced or not. Closes its window once the command ran.
/// </summary>
public sealed partial class AddSubmoduleViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormAddSubmodule";

    private readonly IGitModule _module;
    private readonly IRemoteUrlHistory _remoteUrls;
    private readonly Func<string, Task<bool>> _runGitWithOutput;

    /// <param name="module">The repository the submodule is added to.</param>
    /// <param name="remoteUrls">Upstream's remote URL history, offered as the repositories to add.</param>
    /// <param name="runGitWithOutput">Runs the add command with its output shown (upstream's FormProcess).</param>
    public AddSubmoduleViewModel(IDialogHost host, IGitModule module, IRemoteUrlHistory remoteUrls,
        Func<string, Task<bool>> runGitWithOutput)
        : base(host)
    {
        _module = module;
        _remoteUrls = remoteUrls;
        _runGitWithOutput = runGitWithOutput;
    }

    /// <summary>
    ///  Upstream's <c>Directory</c> list: the remote URL history.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> RemoteHistory { get; private set; } = [];

    /// <summary>
    ///  The repository to add: a URL or a folder.
    /// </summary>
    [ObservableProperty]
    public partial string? RemotePath { get; set; }

    [ObservableProperty]
    public partial string? LocalPath { get; set; }

    /// <summary>
    ///  The remote repository's branches, read each time the list opens.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> RemoteBranches { get; private set; } = [];

    [ObservableProperty]
    public partial string? Branch { get; set; }

    [ObservableProperty]
    public partial bool Force { get; set; }

    /// <summary>
    ///  Picks a folder as the repository to add, starting in the given one (null for none); null when cancelled. The
    ///  window sets it, as it is the platform's picker.
    /// </summary>
    public Func<string?, Task<string?>>? PickFolder { get; set; }

    /// <summary>
    ///  Upstream's constructor: the remote URL history in the list, the boxes empty. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
        RemoteHistory = [.. (await _remoteUrls.LoadAsync()).Select(remote => remote.Path)]);

    /// <summary>
    ///  Upstream's <c>BranchDropDown</c>: the remote repository's branches.
    /// </summary>
    public Task LoadBranchesAsync() => GuardAsync(async () =>
    {
        string? url = RemotePath;
        RemoteBranches = await Task.Run(() => SubmodulesDialog.LoadRemoteBranches(_module.GitExecutable, url));
    });

    // Upstream's DirectoryTextUpdate and DirectorySelectedIndexChanged.
    partial void OnRemotePathChanged(string? value)
    {
        if (SubmodulesDialog.ProposedLocalPath(value) is { } path)
        {
            LocalPath = path;
        }
    }

    // Upstream's BrowseClick: a folder as the repository to add.
    [RelayCommand]
    private Task BrowseAsync() => GuardAsync(async () =>
    {
        string? start = string.IsNullOrWhiteSpace(RemotePath) || !Directory.Exists(RemotePath) ? null : RemotePath;
        if (PickFolder is not null && await PickFolder(start) is { } path)
        {
            RemotePath = path;
        }
    });

    // Upstream's AddClick: both paths are needed; the command runs with its output and the window closes.
    [RelayCommand]
    private Task AddAsync() => GuardAsync(async () =>
    {
        if (!SubmodulesDialog.CanAdd(RemotePath, LocalPath))
        {
            await Host.ShowMessageAsync(Host.Translate(SubmodulesDialog.RemoteAndLocalPathRequired));
            return;
        }

        await _runGitWithOutput(SubmodulesDialog.AddArguments(RemotePath!, LocalPath!, Branch, Force).ToString());
        Host.Close(null);
    });
}
