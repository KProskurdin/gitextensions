using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.Core.RepositoryHosts;

/// <summary>
///  A repository of a repository host as the fork and clone lists show it; a row without a repository is upstream's
///  " : LOADING : " or " : SEARCHING : " placeholder.
/// </summary>
public sealed record HostedRepositoryRow(IHostedRepository? Repo, string Name, string Owner = "", string IsFork = "",
    string Forks = "", string IsPrivate = "");

/// <summary>
///  What <see cref="ForkAndCloneViewModel"/> asks of its window: the folder picker and what follows a clone.
/// </summary>
public sealed record ForkAndCloneServices
{
    /// <summary>
    ///  The folder picker, starting at the folder given (or none); the folder picked, or null.
    /// </summary>
    public Func<string?, Task<string?>>? PickFolder { get; init; }

    /// <summary>
    ///  Upstream's <c>GitModuleChanged</c>: the repository just cloned, which the browse window opens.
    /// </summary>
    public Action<GitModule>? Cloned { get; init; }
}

/// <summary>
///  Upstream's <c>ForkAndCloneForm</c>: the user's repositories on the host and a search of others, forking one, and cloning
///  the selected one with upstream's options (folder, upstream remote, protocol, depth). The host plugin is asked off the UI
///  thread, as upstream does.
/// </summary>
public sealed partial class ForkAndCloneViewModel : RepositoryHostViewModel
{
    public const string Category = "ForkAndCloneForm";

    // The tabs: the user's repositories, the search.
    public const int MyReposTab = 0;
    public const int SearchTab = 1;

    private const string UpstreamRemoteName = "upstream";

    private readonly IRepositoryHostPlugin _gitHoster;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly ForkAndCloneServices _services;

    // True while the clone options are shown again, so the changes that makes do not show them again.
    private bool _updating;

    public ForkAndCloneViewModel(IDialogHost host, ShowRepositoryHostMessage showMessage, IRepositoryHostPlugin gitHoster,
        RepositoryOperationsViewModel actions, string defaultDestination, ForkAndCloneServices services)
        : base(host, Category, showMessage)
    {
        _gitHoster = gitHoster;
        _actions = actions;
        _services = services;
        Destination = defaultDestination;
    }

    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<HostedRepositoryRow> MyRepos { get; private set; } = [];

    [ObservableProperty]
    public partial HostedRepositoryRow? SelectedMyRepo { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<HostedRepositoryRow> SearchResults { get; private set; } = [];

    [ObservableProperty]
    public partial HostedRepositoryRow? SelectedSearchResult { get; set; }

    [ObservableProperty]
    public partial string? SearchText { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    public partial bool IsSearching { get; private set; }

    /// <summary>
    ///  The selected search result's description.
    /// </summary>
    [ObservableProperty]
    public partial string? Description { get; private set; } = "";

    /// <summary>
    ///  The help text under the user's repositories, with the error when they could not be read.
    /// </summary>
    [ObservableProperty]
    public partial string? HelpText { get; set; }

    [ObservableProperty]
    public partial string? Destination { get; set; } = "";

    [ObservableProperty]
    public partial string? CreateDirectory { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<string> UpstreamRemoteChoices { get; private set; } = [];

    [ObservableProperty]
    public partial string? UpstreamRemote { get; set; } = "";

    [ObservableProperty]
    public partial bool CanAddUpstreamRemote { get; private set; } = true;

    [ObservableProperty]
    public partial IReadOnlyList<GitProtocol> Protocols { get; private set; } = [];

    [ObservableProperty]
    public partial GitProtocol? SelectedProtocol { get; set; }

    [ObservableProperty]
    public partial bool IsProtocolVisible { get; private set; }

    [ObservableProperty]
    public partial decimal? Depth { get; set; } = 0;

    [ObservableProperty]
    public partial string CloneInfo { get; private set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloneCommand))]
    public partial bool CanClone { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ForkCommand))]
    public partial bool CanFork { get; private set; }

    private bool CanSearch => !IsSearching;

    private IHostedRepository? CurrentlySelectedGitRepo
        => (SelectedTabIndex == SearchTab ? SelectedSearchResult : SelectedMyRepo)?.Repo;

    /// <summary>
    ///  Upstream's <c>ForkAndCloneForm_Load</c>. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync()
    {
        UpdateCloneInfo();
        return UpdateMyReposAsync();
    }

    partial void OnSelectedTabIndexChanged(int value) => UpdateCloneInfo();

    partial void OnSelectedMyRepoChanged(HostedRepositoryRow? value) => UpdateCloneInfo();

    partial void OnSelectedSearchResultChanged(HostedRepositoryRow? value)
    {
        UpdateCloneInfo();
        CanFork = value?.Repo is not null;
        if (value?.Repo is { } repo)
        {
            Description = repo.Description;
        }
    }

    partial void OnCreateDirectoryChanged(string? value) => UpdateCloneInfo(updateCreateDir: false, updateProtocols: false);

    partial void OnDestinationChanged(string? value) => UpdateCloneInfo(updateCreateDir: false, updateProtocols: false);

    partial void OnUpstreamRemoteChanged(string? value) => UpdateCloneInfo(updateCreateDir: false, updateProtocols: false);

    partial void OnSelectedProtocolChanged(GitProtocol? value)
    {
        if (!_updating && CurrentlySelectedGitRepo is { } repo && value is { } protocol)
        {
            repo.CloneProtocol = protocol;
            ShowCloneInfo(repo);
        }
    }

    // Upstream's UpdateMyRepos.
    private async Task UpdateMyReposAsync()
    {
        MyRepos = [new HostedRepositoryRow(null, Text("_strLoading", " : LOADING : "))];
        try
        {
            IReadOnlyList<IHostedRepository> repos = await Task.Run(() => _gitHoster.GetMyRepos());
            MyRepos = [.. repos.OrderBy(repo => repo.Name)
                .Select(repo => new HostedRepositoryRow(repo, repo.Name, repo.Owner ?? "", YesNo(repo.IsAFork),
                    repo.Forks.ToString(CultureInfo.CurrentCulture), YesNo(repo.IsPrivate)))];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            MyRepos = [];
            HelpText = string.Format(Text("_strFailedToGetRepos",
                    "Failed to get repositories. This most likely means you didn't configure {0}, please do so via the menu \"Plugins/{0}\"."),
                _gitHoster.Name) + $"{Environment.NewLine}{Environment.NewLine}Exception: {ex.Message}{Environment.NewLine}{Environment.NewLine}{HelpText}";
        }
    }

    // Upstream's _searchBtn_Click (false) and _getFromUserBtn_Click (true).
    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync(bool byUser)
    {
        string search = (SearchText ?? "").Trim();
        if (search.Length == 0)
        {
            return;
        }

        IsSearching = true;
        SearchResults = [new HostedRepositoryRow(null, Text("_strSearching", " : SEARCHING : "))];
        try
        {
            IReadOnlyList<IHostedRepository> repos = await Task.Run(() =>
                byUser ? _gitHoster.GetRepositoriesOfUser(search) : _gitHoster.SearchForRepository(search));
            SearchResults = [.. repos.OrderBy(repo => repo.Name)
                .Select(repo => new HostedRepositoryRow(repo, repo.Name, repo.Owner ?? "", YesNo(repo.IsAFork),
                    repo.Forks.ToString(CultureInfo.CurrentCulture)))];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SearchResults = [];
            ShowError(!byUser ? Text("_strSearchFailed", "Search failed!") + Environment.NewLine + ex.Message
                : ex.Message.Contains("404") ? Text("_strUserNotFound", "User not found!")
                : Text("_strCouldNotFetchReposOfUser", "Could not fetch repositories of user!") + Environment.NewLine + ex.Message);
        }
        finally
        {
            IsSearching = false;
        }
    }

    // Upstream's _forkBtn_Click: the user's repositories are read again, with the new fork.
    [RelayCommand(CanExecute = nameof(CanFork))]
    private async Task ForkAsync()
    {
        if (SelectedSearchResult?.Repo is not { } repo)
        {
            ShowError(Text("_strSelectOneItem", "You must select exactly one item"));
            return;
        }

        try
        {
            await Task.Run(repo.Fork);
        }
        catch (Exception ex)
        {
            ShowError(Text("_strFailedToFork", "Failed to fork:") + Environment.NewLine + ex.Message);
        }

        SelectedTabIndex = MyReposTab;
        await UpdateMyReposAsync();
    }

    [RelayCommand]
    private Task BrowseAsync() => GuardAsync(async () =>
    {
        if (_services.PickFolder is { } pick
            && await pick(string.IsNullOrWhiteSpace(Destination) ? null : Destination) is { } folder)
        {
            Destination = folder;
        }
    });

    // Upstream's Clone: an upstream remote is added to the clone when asked for and the repository is a fork.
    [RelayCommand(CanExecute = nameof(CanClone))]
    private Task CloneAsync() => GuardAsync(async () =>
    {
        if (CurrentlySelectedGitRepo is not { } repo || GetTargetDir() is not { } targetDir)
        {
            return;
        }

        int depth = (int)(Depth ?? 0);
        if (!await _actions.CloneAsync(repo.CloneUrl, targetDir, depth > 0 ? depth : null))
        {
            return;
        }

        GitModule module = GitModules.Open(targetDir);
        string upstreamRemote = (UpstreamRemote ?? "").Trim();
        if (upstreamRemote.Length > 0 && !string.IsNullOrEmpty(repo.ParentUrl))
        {
            string error = module.AddRemote(upstreamRemote, repo.ParentUrl);
            if (!string.IsNullOrEmpty(error))
            {
                ShowMessage(error, Text("_strCouldNotAddRemote", "Could not add remote"));
            }
        }

        _services.Cloned?.Invoke(module);
        Host.Close(null);
    });

    // Upstream's openGitupPageBtn: the selected repository's homepage.
    [RelayCommand]
    private void OpenHomepage()
    {
        if (CurrentlySelectedGitRepo is not { } repo)
        {
            return;
        }

        string homepage = repo.Homepage;
        if (string.IsNullOrEmpty(homepage) || !(homepage.StartsWith("http://") || homepage.StartsWith("https://")))
        {
            ShowError(Text("_strNoHomepageDefined", "No homepage defined"));
        }
        else
        {
            Host.OpenUrl(homepage);
        }
    }

    [RelayCommand]
    private void Close() => Host.Close(null);

    // Upstream's UpdateCloneInfo: the selected repository's name as the folder, its parent's owner as the upstream remote,
    // its protocols, and what the clone will do.
    private void UpdateCloneInfo(bool updateCreateDir = true, bool updateProtocols = true)
    {
        if (_updating)
        {
            return;
        }

        _updating = true;
        try
        {
            IHostedRepository? repo = CurrentlySelectedGitRepo;
            if (repo is null)
            {
                IsProtocolVisible = false;
                CanClone = false;
                CloneInfo = "";
                CreateDirectory = "";
                return;
            }

            bool multipleProtocols = repo.SupportedCloneProtocols.Any();
            if (multipleProtocols && updateProtocols)
            {
                GitProtocol current = SelectedProtocol ?? repo.SupportedCloneProtocols[0];
                Protocols = [.. repo.SupportedCloneProtocols];
                if (repo.SupportedCloneProtocols.Contains(current))
                {
                    repo.CloneProtocol = current;
                }

                SelectedProtocol = repo.CloneProtocol;
            }

            IsProtocolVisible = multipleProtocols;
            if (updateCreateDir)
            {
                CreateDirectory = repo.Name;
                UpstreamRemoteChoices = repo.ParentOwner is { } parentOwner ? [parentOwner, UpstreamRemoteName] : [];
                UpstreamRemote = repo.ParentOwner ?? "";
                CanAddUpstreamRemote = repo.ParentOwner is not null;
            }

            CanClone = true;
            ShowCloneInfo(repo);
        }
        finally
        {
            _updating = false;
        }
    }

    private void ShowCloneInfo(IHostedRepository repo)
    {
        string upstreamRemote = (UpstreamRemote ?? "").Trim();
        string moreInfo = upstreamRemote.Length > 0
            ? string.Format(Text("_strWillBeAddedAsARemote", "\"{0}\" will be added as a remote."), upstreamRemote)
            : "";
        string format = SelectedTabIndex == SearchTab
            ? Text("_strWillCloneInfo", "Will clone {0} into {1}.\r\nYou can not push unless you are a collaborator. {2}")
            : Text("_strWillCloneWithPushAccess", "Will clone {0} into {1}.\r\nYou will have push access. {2}");
        CloneInfo = string.Format(format, repo.CloneUrl, TargetDir() ?? "", moreInfo).ReplaceLineEndings();
    }

    // Upstream's GetTargetDir: an empty destination is an error when cloning.
    private string? GetTargetDir()
    {
        if (TargetDir() is { } targetDir)
        {
            return targetDir;
        }

        ShowError(Text("_strCloneFolderCanNotBeEmpty", "Clone folder can not be empty"));
        return null;
    }

    private string? TargetDir()
    {
        string destination = (Destination ?? "").Trim();
        return destination.Length == 0 ? null : Path.Combine(destination, CreateDirectory ?? "");
    }

    private string YesNo(bool value) => value ? Yes : No;
}
