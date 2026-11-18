using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.RepositoryHosts;

/// <summary>
///  Upstream's <c>CreatePullRequestForm</c>: a pull request from a branch of the user's own hosted repository to a branch of
///  another hosted repository of the module (its upstream), with the title taken from the branch's last commit and the body
///  from the repository's <c>.github/PULL_REQUEST_TEMPLATE.md</c>. The host plugin is asked off the UI thread.
/// </summary>
public sealed partial class CreatePullRequestViewModel : RepositoryHostViewModel
{
    public const string Category = "CreatePullRequestForm";

    private readonly IRepositoryHostPlugin _repoHost;
    private readonly IGitModule _module;
    private readonly string? _chooseRemote;
    private IReadOnlyList<IHostedRemote> _hostedRemotes = [];

    // The title last suggested (or typed when the window opened): a title the user changed since is kept.
    private string? _suggestedTitle = "";

    public CreatePullRequestViewModel(IDialogHost host, ShowRepositoryHostMessage showMessage,
        IRepositoryHostPlugin repoHost, IGitModule module, string? chooseRemote = null)
        : base(host, Category, showMessage)
    {
        _repoHost = repoHost;
        _module = module;
        _chooseRemote = chooseRemote;
    }

    /// <summary>
    ///  The hosted repositories a pull request can go to: those not owned by the user.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<IHostedRemote> Targets { get; private set; } = [];

    [ObservableProperty]
    public partial IHostedRemote? SelectedTarget { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> TargetBranches { get; private set; } = [];

    [ObservableProperty]
    public partial string? SelectedTargetBranch { get; set; }

    /// <summary>
    ///  The target branch's box, which can be typed in.
    /// </summary>
    [ObservableProperty]
    public partial string? TargetBranchText { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<string> MyBranches { get; private set; } = [];

    [ObservableProperty]
    public partial string? SelectedMyBranch { get; set; }

    [ObservableProperty]
    public partial string? MyBranchText { get; set; } = "";

    [ObservableProperty]
    public partial string? PullRequestTitle { get; set; } = "";

    [ObservableProperty]
    public partial string? Body { get; set; } = "";

    /// <summary>
    ///  Upstream enables Create once a repository's branches are listed.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial bool CanCreate { get; private set; }

    /// <summary>
    ///  True once the target and the user's branches are listed.
    /// </summary>
    public bool AreBranchesLoaded { get; private set; }

    private string Loading => Text("_strLoading", "Loading...");

    private IHostedRemote? MyRemote => _hostedRemotes.FirstOrDefault(remote => remote.IsOwnedByMe);

    /// <summary>
    ///  Upstream's <c>CreatePullRequestForm_Load</c>: without a hosted repository owned by someone else there is no target,
    ///  and the window closes. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        _suggestedTitle = PullRequestTitle;
        MyBranchText = Loading;
        _hostedRemotes = await Task.Run(() => _repoHost.GetHostedRemotesForModule());
        IHostedRemote[] foreignHostedRemotes = [.. _hostedRemotes.Where(remote => !remote.IsOwnedByMe)];
        if (foreignHostedRemotes.Length == 0)
        {
            ShowMessage(Text("_strFailedToCreatePullRequest", "Failed to create pull request.") + Environment.NewLine
                + Text("_strPleaseCloneGitHubRep", "Please clone GitHub repository before pull request."), "");
            Host.Close(null);
            return;
        }

        Targets = foreignHostedRemotes;
        SelectedTarget = foreignHostedRemotes.FirstOrDefault(remote => remote.Name == _chooseRemote)
            ?? foreignHostedRemotes[0];
        Task myBranches = MyRemote is { } myRemote ? LoadBranchesAsync(myRemote, mine: true) : Task.CompletedTask;
        string templatePath = Path.Join(_module.WorkingDir, ".github", "PULL_REQUEST_TEMPLATE.md");
        if (File.Exists(templatePath))
        {
            try
            {
                Body = await File.ReadAllTextAsync(templatePath);
            }
            catch (IOException ex)
            {
                ShowError(Text("_strFailedToLoadTemplate", "Failed to load PR template from file.") + Environment.NewLine
                    + ex.Message);
            }
        }

        await myBranches;
        AreBranchesLoaded = true;
    });

    partial void OnSelectedTargetChanged(IHostedRemote? value)
    {
        if (value is not null)
        {
            Run(() => LoadBranchesAsync(value, mine: false));
        }
    }

    partial void OnSelectedMyBranchChanged(string? value) => Run(SuggestTitleAsync);

    // Upstream's PopulateBranchesComboAndEnableCreateButton: the hosted repository's branches with its default one chosen.
    private async Task LoadBranchesAsync(IHostedRemote remote, bool mine)
    {
        SetBranches(mine, [], null, Loading);
        try
        {
            (IReadOnlyList<IHostedBranch> branches, string defaultBranch) = await Task.Run(() =>
            {
                IHostedRepository hostedRepository = remote.GetHostedRepository();
                return (hostedRepository.GetBranches(), hostedRepository.GetDefaultBranch());
            });
            List<string> names = [.. branches.Select(branch => branch.Name)];
            string? selected = names.Contains(defaultBranch) ? defaultBranch : names.FirstOrDefault();
            SetBranches(mine, names, selected, selected ?? "");
            CanCreate = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SetBranches(mine, [], null, "");
            ShowMessage(string.Format(RemoteInError, ex.Message, remote.DisplayData),
                Text("_strRemoteFailToLoadBranches", "Fail to load target branches"));
        }
    }

    // The list first, then the choice, then the text: an editable list clears its text when its items change.
    private void SetBranches(bool mine, IReadOnlyList<string> names, string? selected, string text)
    {
        if (mine)
        {
            MyBranches = names;
            SelectedMyBranch = selected;
            MyBranchText = text;
        }
        else
        {
            TargetBranches = names;
            SelectedTargetBranch = selected;
            TargetBranchText = text;
        }
    }

    // Upstream's _yourBranchCB_SelectedIndexChanged: until the user types a title, it is the subject of the branch's last
    // commit (on the user's remote).
    private async Task SuggestTitleAsync()
    {
        if (_suggestedTitle != PullRequestTitle || SelectedMyBranch is not { } branch || MyRemote is not { } myRemote)
        {
            return;
        }

        string revision = myRemote.Name.Combine("/", branch)!;
        string? lastMessage = await Task.Run(() =>
            _module.GetPreviousCommitMessages(count: 1, revision: revision, authorPattern: string.Empty).FirstOrDefault());
        PullRequestTitle = lastMessage?.SubstringUntil('\n');
        _suggestedTitle = PullRequestTitle;
    }

    // Upstream's _createBtn_Click.
    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        if (SelectedTarget is not { } remote)
        {
            return;
        }

        string title = (PullRequestTitle ?? "").Trim();
        string body = (Body ?? "").Trim();
        if (title.Length == 0)
        {
            ShowError(Text("_strYouMustSpecifyATitle", "You must specify a title."));
            return;
        }

        string myBranch = MyBranchText ?? "";
        string remoteBranch = TargetBranchText ?? "";
        try
        {
            await Task.Run(() => remote.GetHostedRepository().CreatePullRequest(myBranch, remoteBranch, title, body));
            ShowMessage(Text("_strDone", "Done"), Text("_strPullRequest", "Pull request"), isError: false);
            Host.Close(null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowError(Text("_strFailedToCreatePullRequest", "Failed to create pull request.") + Environment.NewLine
                + ex.Message);
        }
    }
}
