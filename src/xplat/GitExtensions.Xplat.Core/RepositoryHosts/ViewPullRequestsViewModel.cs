using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Presentation;
using GitExtUtils;
using GitUIPluginInterfaces.RepositoryHosts;

namespace GitExtensions.Xplat.Core.RepositoryHosts;

/// <summary>
///  A pull request as the list shows it; one without information is upstream's " : LOADING : " placeholder.
/// </summary>
public sealed record PullRequestRow(IPullRequestInformation? Info, string Id, string Title, string Owner = "",
    string Created = "", string FetchBranch = "");

/// <summary>
///  A comment or commit of a pull request's discussion, as upstream's <c>DiscussionHtmlCreator</c> shows it: a commit's
///  author in red, a comment's in the highlight color.
/// </summary>
public sealed record DiscussionEntryRow(string Author, string Created, string? Commit, string Body)
{
    public bool IsCommit => Commit is not null;
}

/// <summary>
///  A file's part of a pull request's diff, which the viewer shows.
/// </summary>
public sealed record PullRequestPatch(string Name, string Patch);

/// <summary>
///  Upstream's <c>ViewPullRequestsForm</c>: the pull requests of the repository's hosted remotes, the selected one's diff
///  (split into files as upstream does) and discussion, and fetching it to its pr/ branch, adding its owner as a remote, or
///  closing it. The host plugin is asked off the UI thread.
/// </summary>
public sealed partial class ViewPullRequestsViewModel : RepositoryHostViewModel
{
    public const string Category = "ViewPullRequestsForm";

    private readonly IRepositoryHostPlugin _gitHoster;
    private readonly IGitModule _module;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly Dictionary<string, string> _diffCache = [];
    private IPullRequestDiscussion? _discussion;
    private GitProtocol _cloneGitProtocol;
    private bool _isFirstLoad = true;

    public ViewPullRequestsViewModel(IDialogHost host, ShowRepositoryHostMessage showMessage,
        IRepositoryHostPlugin gitHoster, IGitModule module, RepositoryOperationsViewModel actions)
        : base(host, Category, showMessage)
    {
        _gitHoster = gitHoster;
        _module = module;
        _actions = actions;
    }

    /// <summary>
    ///  Raised when the discussion was shown again: the view scrolls to its last entry, as upstream's page does.
    /// </summary>
    public event EventHandler? DiscussionShown;

    [ObservableProperty]
    public partial IReadOnlyList<IHostedRemote> HostedRemotes { get; private set; } = [];

    [ObservableProperty]
    public partial IHostedRemote? SelectedHostedRemote { get; set; }

    /// <summary>
    ///  The list of hosted repositories can be changed while no pull requests are read.
    /// </summary>
    [ObservableProperty]
    public partial bool CanChooseHostedRemote { get; private set; } = true;

    [ObservableProperty]
    public partial string StatusText { get; private set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<PullRequestRow> PullRequests { get; private set; } = [];

    [ObservableProperty]
    public partial PullRequestRow? SelectedPullRequest { get; set; }

    /// <summary>
    ///  The files of the selected pull request's diff.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Files { get; private set; } = [];

    [ObservableProperty]
    public partial string? SelectedFile { get; set; }

    /// <summary>
    ///  The selected file's part of the diff; null while none is shown.
    /// </summary>
    [ObservableProperty]
    public partial PullRequestPatch? ShownPatch { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<DiscussionEntryRow> Discussion { get; private set; } = [];

    [ObservableProperty]
    public partial string? CommentText { get; set; } = "";

    /// <summary>
    ///  The buttons that act on the selected pull request are enabled while one is selected.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FetchCommand), nameof(AddRemoteAndFetchCommand), nameof(ClosePullRequestCommand),
        nameof(RefreshDiscussionCommand), nameof(PostCommentCommand))]
    public partial IPullRequestInformation? CurrentPullRequest { get; private set; }

    private bool HasPullRequest => CurrentPullRequest is not null;

    private string Loading => Text("_strLoading", " : LOADING : ");

    [GeneratedRegex(@"(?:\n|^)diff --git ", RegexOptions.ExplicitCapture)]
    private static partial Regex DiffCommandRegex { get; }

    [GeneratedRegex(@"^a/([^\n]+) b/(?<name>[^\n]+)\s*(?<value>.*)$", RegexOptions.Singleline | RegexOptions.ExplicitCapture)]
    private static partial Regex FilePartRegex { get; }

    /// <summary>
    ///  Upstream's <c>ViewPullRequestsForm_Load</c>: every hosted remote is read now, off the UI thread; one that fails is
    ///  reported and left in the list, as upstream does. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        StatusText = Loading;
        IReadOnlyList<IHostedRemote> hostedRemotes = await Task.Run(() => _gitHoster.GetHostedRemotesForModule());
        foreach (IHostedRemote hostedRemote in hostedRemotes)
        {
            try
            {
                await Task.Run(hostedRemote.GetHostedRepository);
            }
            catch (Exception ex)
            {
                ShowMessage(string.Format(RemoteInError, ex.Message, hostedRemote.DisplayData),
                    Text("_strRemoteIgnore", "Remote ignored"));
            }
        }

        StatusText = "";

        // Upstream's SelectHostedRepositoryForCurrentRemote: the current branch's remote, or the first one; its URL decides
        // whether pull requests are fetched over HTTPS or SSH.
        string currentRemote = await Task.Run(_module.GetCurrentRemote);
        IReadOnlyList<Remote> remotes = await Task.Run(_module.GetRemotesAsync);
        Remote remote = remotes.FirstOrDefault(r => string.IsNullOrEmpty(currentRemote) || r.Name == currentRemote);
        _cloneGitProtocol = remote.FetchUrl?.IsUrlUsingHttp() == true ? GitProtocol.Https : GitProtocol.Ssh;
        HostedRemotes = hostedRemotes;
        SelectedHostedRemote = hostedRemotes.FirstOrDefault(hosted =>
                string.Equals(hosted.Name, currentRemote, StringComparison.OrdinalIgnoreCase))
            ?? hostedRemotes.FirstOrDefault();
    });

    partial void OnSelectedHostedRemoteChanged(IHostedRemote? value) => Run(LoadPullRequestsAsync);

    partial void OnSelectedPullRequestChanged(PullRequestRow? value)
    {
        IPullRequestInformation? info = value?.Info;
        if (ReferenceEquals(info, CurrentPullRequest))
        {
            return;
        }

        CurrentPullRequest = info;
        _discussion = null;
        Discussion = [];
        ShownPatch = null;
        Files = [];
        if (info is null)
        {
            return;
        }

        info.HeadRepo.CloneProtocol = _cloneGitProtocol;
        Run(() => LoadDiffPatchAsync(info));
        Run(() => LoadDiscussionAsync(forceReload: false));
    }

    partial void OnSelectedFileChanged(string? value)
        => ShownPatch = value is not null && _diffCache.TryGetValue(value, out string? patch)
            ? new PullRequestPatch(value, patch)
            : null;

    // Upstream's _selectedOwner_SelectedIndexChanged and SetPullRequestsData: on the first load, a repository without pull
    // requests (or one that cannot be read) moves on to the next one.
    private async Task LoadPullRequestsAsync()
    {
        if (SelectedHostedRemote is not { } hostedRemote)
        {
            return;
        }

        SelectedPullRequest = null;
        PullRequests = [new PullRequestRow(null, "", Loading)];
        IReadOnlyList<IPullRequestInformation> pullRequests;
        CanChooseHostedRemote = false;
        try
        {
            IHostedRepository hostedRepo = await Task.Run(hostedRemote.GetHostedRepository);
            pullRequests = await Task.Run(hostedRepo.GetPullRequests);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CanChooseHostedRemote = true;
            if (_isFirstLoad && SelectNextHostedRepository())
            {
                return;
            }

            PullRequests = [];
            ShowError(Text("_strFailedToFetchPullData", "Failed to fetch pull data!") + Environment.NewLine + ex.Message);
            return;
        }

        CanChooseHostedRemote = true;
        if (_isFirstLoad)
        {
            if (pullRequests.Count == 0 && SelectNextHostedRepository())
            {
                return;
            }

            _isFirstLoad = false;
        }

        List<PullRequestRow> rows = [.. pullRequests.Select(info => new PullRequestRow(info, info.Id, info.Title, info.Owner,
            info.Created.ToString(CultureInfo.CurrentCulture), info.FetchBranch))];
        PullRequests = rows;
        SelectedPullRequest = rows.FirstOrDefault();
    }

    private bool SelectNextHostedRepository()
    {
        int next = SelectedHostedRemote is { } current ? HostedRemotes.ToList().IndexOf(current) + 1 : 0;
        if (next >= HostedRemotes.Count)
        {
            return false;
        }

        SelectedHostedRemote = HostedRemotes[next];
        return true;
    }

    private async Task LoadDiffPatchAsync(IPullRequestInformation info)
    {
        try
        {
            string content = await info.GetDiffDataAsync();
            if (ReferenceEquals(info, CurrentPullRequest))
            {
                SplitAndLoadDiff(content);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowError(Text("_strFailedToLoadDiffData", "Failed to load diff data!") + Environment.NewLine + ex.Message);
        }
    }

    // Upstream's SplitAndLoadDiff: one entry per "diff --git" part, named after its "b/" path.
    private void SplitAndLoadDiff(string diffData)
    {
        _diffCache.Clear();
        List<string> names = [];
        foreach (string part in DiffCommandRegex.Split(diffData).Where(part => part?.Trim().Length is > 10))
        {
            Match match = FilePartRegex.Match(part);
            if (!match.Success)
            {
                ShowError(Text("_strUnableUnderstandPatch", "Error: Unable to understand patch"));
                return;
            }

            string name = match.Groups["name"].Value.Trim();
            names.Add(name);
            _diffCache[name] = match.Groups["value"].Value;
        }

        Files = names;
        SelectedFile = names.FirstOrDefault();
    }

    private async Task LoadDiscussionAsync(bool forceReload)
    {
        if (CurrentPullRequest is not { } info)
        {
            return;
        }

        try
        {
            IPullRequestDiscussion discussion = await Task.Run(() =>
            {
                IPullRequestDiscussion loaded = _discussion is not null && forceReload ? _discussion : info.GetDiscussion();
                if (forceReload)
                {
                    loaded.ForceReload();
                }

                return loaded;
            });
            if (ReferenceEquals(info, CurrentPullRequest))
            {
                _discussion = discussion;
                ShowDiscussion(discussion.Entries);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowError(Text("_strCouldNotLoadDiscussion", "Could not load discussion!") + Environment.NewLine + ex.Message);
            ShowDiscussion([]);
        }
    }

    private void ShowDiscussion(IReadOnlyList<IDiscussionEntry> entries)
    {
        Discussion = [.. entries.Select(entry => new DiscussionEntryRow(entry.Author ?? "[UNKNOWN]",
            entry.Created.ToString(CultureInfo.CurrentCulture),
            entry is ICommitDiscussionEntry commit ? $"Commit:  {commit.Sha ?? "[UNKNOWN]"}" : null,
            entry.Body ?? "[UNKNOWN]"))];
        DiscussionShown?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(HasPullRequest))]
    private Task RefreshDiscussionAsync() => LoadDiscussionAsync(forceReload: true);

    // Upstream has the comment box and its buttons without handlers; here they post and reload the discussion through the
    // plugin's IPullRequestDiscussion.
    [RelayCommand(CanExecute = nameof(HasPullRequest))]
    private async Task PostCommentAsync()
    {
        string text = (CommentText ?? "").Trim();
        if (_discussion is not { } discussion || text.Length == 0)
        {
            return;
        }

        try
        {
            await Task.Run(() => discussion.Post(text));
            CommentText = "";
            await LoadDiscussionAsync(forceReload: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowError(Text("_strFailedToLoadDiscussionItem", "Failed to post discussion item!") + Environment.NewLine + ex.Message);
        }
    }

    // Upstream's _fetchBtn_Click: the pull request's head into its pr/ branch; the window closes when it worked.
    [RelayCommand(CanExecute = nameof(HasPullRequest))]
    private Task FetchAsync() => GuardAsync(async () =>
    {
        if (CurrentPullRequest is not { } info)
        {
            return;
        }

        GitArgumentBuilder fetch = new("fetch")
        {
            "--no-tags",
            "--progress",
            info.HeadRepo.CloneUrl.Quote(),
            $"{info.HeadRef}:{info.FetchBranch}".Quote(),
        };
        if (await _actions.RunProgramAsync($"Fetch {info.FetchBranch}", AppSettings.GitCommand, fetch.ToString(),
                _module.WorkingDir))
        {
            Host.Close(null);
        }
    });

    // Upstream's _addAsRemoteAndFetch_Click: the owner's repository as a remote (unless a remote of that name points at it
    // already), then a fetch of the pull request's branch and a checkout of it.
    [RelayCommand(CanExecute = nameof(HasPullRequest))]
    private Task AddRemoteAndFetchAsync() => GuardAsync(async () =>
    {
        if (CurrentPullRequest is not { } info)
        {
            return;
        }

        string remoteName = info.Owner;
        string remoteUrl = info.HeadRepo.CloneUrl;
        string remoteRef = info.HeadRef;
        if (HostedRemotes.FirstOrDefault(remote => remote.Name == remoteName) is { } existing)
        {
            IHostedRepository hostedRepository = await Task.Run(existing.GetHostedRepository);
            hostedRepository.CloneProtocol = _cloneGitProtocol;
            if (hostedRepository.CloneUrl != remoteUrl)
            {
                ShowError(string.Format(Text("_strRemoteAlreadyExist",
                        "ERROR: Remote with name {0} already exists but it points to a different repository!\r\nDetails: Is {1} expected {2}"),
                    remoteName, hostedRepository.CloneUrl, remoteUrl).ReplaceLineEndings());
                return;
            }
        }
        else
        {
            string error = _module.AddRemote(remoteName, remoteUrl);
            if (!string.IsNullOrEmpty(error))
            {
                ShowMessage(error, string.Format(Text("_strCouldNotAddRemote", "Could not add remote with name {0} and URL {1}"),
                    remoteName, remoteUrl));
                return;
            }
        }

        GitArgumentBuilder fetch = new("fetch")
        {
            "--no-tags",
            "--progress",
            remoteName.Quote(),
            $"{remoteRef}:{remoteName}/{remoteRef}".Quote(),
        };
        if (!await _actions.RunProgramAsync($"Fetch {remoteName}/{remoteRef}", AppSettings.GitCommand, fetch.ToString(),
                _module.WorkingDir))
        {
            return;
        }

        await _actions.CheckoutAsync(_module.WorkingDir, $"{remoteName}/{remoteRef}");
        Host.Close(null);
    });

    [RelayCommand(CanExecute = nameof(HasPullRequest))]
    private async Task ClosePullRequestAsync()
    {
        if (CurrentPullRequest is not { } info)
        {
            return;
        }

        try
        {
            await Task.Run(info.Close);
            CurrentPullRequest = null;
            await LoadPullRequestsAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowError(Text("_strFailedToClosePullRequest", "Failed to close pull request!") + Environment.NewLine + ex.Message);
        }
    }
}
