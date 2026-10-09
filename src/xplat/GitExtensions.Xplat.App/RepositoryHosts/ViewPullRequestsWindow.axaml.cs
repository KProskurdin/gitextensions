using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Media;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Ui;
using GitExtUtils;
using GitUIPluginInterfaces.RepositoryHosts;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;

namespace GitExtensions.Xplat.App.RepositoryHosts;

/// <summary>
///  A pull request as the list shows it; one without information is upstream's " : LOADING : " placeholder.
/// </summary>
public sealed record PullRequestRow(IPullRequestInformation? Info, string Id, string Title, string Owner = "",
    string Created = "", string FetchBranch = "");

/// <summary>
///  A comment or commit of a pull request's discussion, as upstream's <c>DiscussionHtmlCreator</c> shows it.
/// </summary>
public sealed record DiscussionEntryRow(string Author, string Created, string? Commit, string Body, IBrush AuthorBrush);

/// <summary>
///  The new shell's version of upstream's <c>ViewPullRequestsForm</c>: the pull requests of the repository's hosted remotes,
///  the selected one's diff (split into files as upstream does) and discussion, and fetching it to its pr/ branch, adding
///  its owner as a remote, or closing it. Upstream shows the discussion as HTML; here it is a list of entries.
/// </summary>
public partial class ViewPullRequestsWindow : Window
{
    private const string Category = "ViewPullRequestsForm";

    private readonly IRepositoryHostPlugin _gitHoster;
    private readonly IGitModule _module;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly Dictionary<string, string> _diffCache = [];
    private IReadOnlyList<IHostedRemote> _hostedRemotes = [];
    private IPullRequestInformation? _currentPullRequestInfo;
    private IPullRequestDiscussion? _discussion;
    private GitProtocol _cloneGitProtocol;
    private bool _isFirstLoad = true;

    public ViewPullRequestsWindow(IRepositoryHostPlugin gitHoster, IGitModule module, RepositoryOperationsViewModel actions)
    {
        _gitHoster = gitHoster;
        _module = module;
        _actions = actions;
        InitializeComponent();
        UpstreamTranslation.Apply(this, Category);
        _selectHostedRepoCB.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(IHostedRemote.DisplayData));
        _selectHostedRepoCB.SelectionChanged += (_, _) => _ = LoadPullRequestsAsync();
        _pullRequestsList.SelectionChanged += (_, _) => OnPullRequestSelected();
        _fileStatusList.SelectionChanged += (_, _) => ShowSelectedFile();
        _fetchBtn.Click += (_, _) => _ = FetchAsync();
        _addAndFetchBtn.Click += (_, _) => _ = AddRemoteAndFetchAsync();
        _closePullRequestBtn.Click += (_, _) => _ = ClosePullRequestAsync();
        _refreshCommentsBtn.Click += (_, _) => _ = LoadDiscussionAsync(forceReload: true);
        _postComment.Click += (_, _) => _ = PostCommentAsync();
        Opened += (_, _) => _ = LoadHostedRemotesAsync();
    }

    [GeneratedRegex(@"(?:\n|^)diff --git ", RegexOptions.ExplicitCapture)]
    private static partial Regex DiffCommandRegex { get; }

    [GeneratedRegex(@"^a/([^\n]+) b/(?<name>[^\n]+)\s*(?<value>.*)$", RegexOptions.Singleline | RegexOptions.ExplicitCapture)]
    private static partial Regex FilePartRegex { get; }

    /// <summary>
    ///  The pull requests shown, for tests.
    /// </summary>
    public IReadOnlyList<PullRequestRow> PullRequests => _pullRequestsList.ItemsSource as IReadOnlyList<PullRequestRow> ?? [];

    private static string Text(string name, string english) => UpstreamTranslation.Text(Category, name, english);

    private WindowOwner MessageOwner => new(this);

    // Upstream's ViewPullRequestsForm_Load: every hosted remote is read now, off the UI thread; one that fails is reported
    // and left in the list, as upstream does.
    private async Task LoadHostedRemotesAsync()
    {
        StatusText.Text = Text("_strLoading", " : LOADING : ");
        IReadOnlyList<IHostedRemote> hostedRemotes = await Task.Run(() => _gitHoster.GetHostedRemotesForModule());
        foreach (IHostedRemote hostedRemote in hostedRemotes)
        {
            try
            {
                await Task.Run(hostedRemote.GetHostedRepository);
            }
            catch (Exception ex)
            {
                MessageBoxes.Show(MessageOwner, string.Format(RepositoryHostTexts.RemoteInError, ex.Message, hostedRemote.DisplayData),
                    Text("_strRemoteIgnore", "Remote ignored"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        StatusText.Text = "";
        _hostedRemotes = hostedRemotes;
        await SelectHostedRepositoryForCurrentRemoteAsync();
    }

    // Upstream's SelectHostedRepositoryForCurrentRemote: the current branch's remote, or the first one; its URL decides
    // whether pull requests are fetched over HTTPS or SSH.
    private async Task SelectHostedRepositoryForCurrentRemoteAsync()
    {
        string currentRemote = await Task.Run(_module.GetCurrentRemote);
        IReadOnlyList<Remote> remotes = await Task.Run(_module.GetRemotesAsync);
        Remote remote = remotes.FirstOrDefault(r => string.IsNullOrEmpty(currentRemote) || r.Name == currentRemote);
        _cloneGitProtocol = remote.FetchUrl?.IsUrlUsingHttp() == true ? GitProtocol.Https : GitProtocol.Ssh;
        _selectHostedRepoCB.ItemsSource = _hostedRemotes;
        _selectHostedRepoCB.SelectedItem = _hostedRemotes.FirstOrDefault(hosted =>
                string.Equals(hosted.Name, currentRemote, StringComparison.OrdinalIgnoreCase))
            ?? _hostedRemotes.FirstOrDefault();
    }

    // Upstream's _selectedOwner_SelectedIndexChanged and SetPullRequestsData: on the first load, a repository without pull
    // requests (or one that cannot be read) moves on to the next one.
    private async Task LoadPullRequestsAsync()
    {
        if (_selectHostedRepoCB.SelectedItem is not IHostedRemote hostedRemote)
        {
            return;
        }

        ShowLoadingPullRequests();
        IHostedRepository hostedRepo;
        IReadOnlyList<IPullRequestInformation> pullRequests;
        _selectHostedRepoCB.IsEnabled = false;
        try
        {
            hostedRepo = await Task.Run(hostedRemote.GetHostedRepository);
            pullRequests = await Task.Run(hostedRepo.GetPullRequests);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _selectHostedRepoCB.IsEnabled = true;
            if (_isFirstLoad && SelectNextHostedRepository())
            {
                return;
            }

            _pullRequestsList.ItemsSource = null;
            ShowError(Text("_strFailedToFetchPullData", "Failed to fetch pull data!") + Environment.NewLine + ex.Message);
            return;
        }

        _selectHostedRepoCB.IsEnabled = true;
        if (_isFirstLoad)
        {
            if (pullRequests.Count == 0 && SelectNextHostedRepository())
            {
                return;
            }

            _isFirstLoad = false;
        }

        List<PullRequestRow> rows = [.. pullRequests.Select(info =>
            new PullRequestRow(info, info.Id, info.Title, info.Owner, info.Created.ToString(CultureInfo.CurrentCulture), info.FetchBranch))];
        _pullRequestsList.ItemsSource = rows;
        _pullRequestsList.SelectedIndex = rows.Count > 0 ? 0 : -1;
    }

    private bool SelectNextHostedRepository()
    {
        int next = _selectHostedRepoCB.SelectedIndex + 1;
        if (next >= _hostedRemotes.Count)
        {
            return false;
        }

        _selectHostedRepoCB.SelectedIndex = next;
        return true;
    }

    private void ShowLoadingPullRequests()
    {
        _currentPullRequestInfo = null;
        _discussionWB.ItemsSource = null;
        _diffViewer.Clear();
        _fileStatusList.ItemsSource = null;
        _pullRequestsList.ItemsSource = new[] { new PullRequestRow(null, "", Text("_strLoading", " : LOADING : ")) };
        UpdateButtons();
    }

    private void OnPullRequestSelected()
    {
        IPullRequestInformation? info = (_pullRequestsList.SelectedItem as PullRequestRow)?.Info;
        if (ReferenceEquals(info, _currentPullRequestInfo))
        {
            return;
        }

        _currentPullRequestInfo = info;
        _discussion = null;
        _discussionWB.ItemsSource = null;
        _diffViewer.Clear();
        _fileStatusList.ItemsSource = null;
        UpdateButtons();
        if (info is null)
        {
            return;
        }

        info.HeadRepo.CloneProtocol = _cloneGitProtocol;
        _ = LoadDiffPatchAsync(info);
        _ = LoadDiscussionAsync(forceReload: false);
    }

    private void UpdateButtons()
    {
        bool selected = _currentPullRequestInfo is not null;
        _fetchBtn.IsEnabled = _addAndFetchBtn.IsEnabled = _closePullRequestBtn.IsEnabled = selected;
        _refreshCommentsBtn.IsEnabled = _postComment.IsEnabled = selected;
    }

    private async Task LoadDiffPatchAsync(IPullRequestInformation info)
    {
        try
        {
            string content = await info.GetDiffDataAsync();
            if (ReferenceEquals(info, _currentPullRequestInfo))
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

        _fileStatusList.ItemsSource = names;
        _fileStatusList.SelectedIndex = names.Count > 0 ? 0 : -1;
    }

    private void ShowSelectedFile()
    {
        if (_fileStatusList.SelectedItem is string name && _diffCache.TryGetValue(name, out string? patch))
        {
            _diffViewer.ShowPatch(name, patch);
        }
    }

    private async Task LoadDiscussionAsync(bool forceReload)
    {
        if (_currentPullRequestInfo is not { } info)
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
            if (ReferenceEquals(info, _currentPullRequestInfo))
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

    // As upstream's DiscussionHtmlCreator: the author in the highlight color, a commit's author in red, then the body; the
    // view scrolls to the last entry, as upstream's does once the page is loaded.
    private void ShowDiscussion(IReadOnlyList<IDiscussionEntry> entries)
    {
        _discussionWB.ItemsSource = entries.Select(entry => entry is ICommitDiscussionEntry commit
                ? new DiscussionEntryRow(entry.Author ?? "[UNKNOWN]", entry.Created.ToString(CultureInfo.CurrentCulture), $"Commit:  {commit.Sha ?? "[UNKNOWN]"}",
                    entry.Body ?? "[UNKNOWN]", Brushes.Red)
                : new DiscussionEntryRow(entry.Author ?? "[UNKNOWN]", entry.Created.ToString(CultureInfo.CurrentCulture), null, entry.Body ?? "[UNKNOWN]",
                    Brushes.RoyalBlue))
            .ToList();
        DiscussionScroll.ScrollToEnd();
    }

    // Upstream has the comment box and its buttons without handlers; here they post and reload the discussion through the
    // plugin's IPullRequestDiscussion.
    private async Task PostCommentAsync()
    {
        string text = (_postCommentText.Text ?? "").Trim();
        if (_discussion is not { } discussion || text.Length == 0)
        {
            return;
        }

        try
        {
            await Task.Run(() => discussion.Post(text));
            _postCommentText.Text = "";
            await LoadDiscussionAsync(forceReload: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowError(Text("_strFailedToLoadDiscussionItem", "Failed to post discussion item!") + Environment.NewLine + ex.Message);
        }
    }

    // Upstream's _fetchBtn_Click: the pull request's head into its pr/ branch; the window closes when it worked.
    private async Task FetchAsync()
    {
        if (_currentPullRequestInfo is not { } info)
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
        if (await _actions.RunProgramAsync($"Fetch {info.FetchBranch}", AppSettings.GitCommand, fetch.ToString(), _module.WorkingDir))
        {
            Close();
        }
    }

    // Upstream's _addAsRemoteAndFetch_Click: the owner's repository as a remote (unless a remote of that name points at it
    // already), then a fetch of the pull request's branch and a checkout of it.
    private async Task AddRemoteAndFetchAsync()
    {
        if (_currentPullRequestInfo is not { } info)
        {
            return;
        }

        string remoteName = info.Owner;
        string remoteUrl = info.HeadRepo.CloneUrl;
        string remoteRef = info.HeadRef;
        if (_hostedRemotes.FirstOrDefault(remote => remote.Name == remoteName) is { } existing)
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
                MessageBoxes.Show(MessageOwner, error,
                    string.Format(Text("_strCouldNotAddRemote", "Could not add remote with name {0} and URL {1}"), remoteName, remoteUrl),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        Close();
    }

    private async Task ClosePullRequestAsync()
    {
        if (_currentPullRequestInfo is not { } info)
        {
            return;
        }

        try
        {
            await Task.Run(info.Close);
            _currentPullRequestInfo = null;
            await LoadPullRequestsAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowError(Text("_strFailedToClosePullRequest", "Failed to close pull request!") + Environment.NewLine + ex.Message);
        }
    }

    private void ShowError(string message)
        => MessageBoxes.Show(MessageOwner, message, RepositoryHostTexts.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
}
