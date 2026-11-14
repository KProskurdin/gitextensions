using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormPush</c> (rules in <see cref="PushDialog"/>): where to push (a remote or a URL),
///  and a branch, a tag, or several branches. Closes its window with the <see cref="PushRequest"/> to run, or null. The
///  checks before pushing are upstream's: the choices are complete, a branch new for the remote is confirmed, a tracking
///  reference is offered, and a force push is offered as force with lease.
/// </summary>
public sealed partial class PushViewModel : DialogViewModel
{
    private readonly IAppPreferences _preferences;
    private readonly IRemoteUrlHistory _remoteUrls;
    private readonly Func<Task>? _openPull;
    private readonly Func<string?, Task<PushRepository>>? _manageRemotes;
    private readonly Func<string, Task>? _loadSshKey;
    private readonly Func<string, Task<IReadOnlyList<IGitRef>?>>? _readRemoteHeads;
    private readonly Func<Task<bool>> _confirmNewBranch;
    private bool _showing;
    private string? _rowsRemote;

    /// <param name="dialog">The dialog state.</param>
    /// <param name="preferences">Upstream's recursive submodules choice, advanced options and confirmations.</param>
    /// <param name="remoteUrls">The remote URL history the URL box lists and a URL push is added to.</param>
    /// <param name="workingDirectory">The repository, for the title.</param>
    /// <param name="canCreatePullRequest">True when a repository host plugin knows the repository; a remote on Azure
    ///  DevOps enables the choice too, as upstream's.</param>
    /// <param name="openPull">Opens the pull dialog (upstream's Pull button).</param>
    /// <param name="manageRemotes">Opens the remotes window with a remote chosen, and reads the repository again
    ///  (upstream's Manage remotes).</param>
    /// <param name="loadSshKey">Loads the remote's PuTTY key.</param>
    /// <param name="readRemoteHeads">Lists the remote's branches as git's ls-remote does; null to use the remote branches
    ///  the repository knows.</param>
    /// <param name="confirmNewBranch">Upstream's confirmation of a branch new for the remote, which can be turned
    ///  off.</param>
    public PushViewModel(IDialogHost host, PushDialog dialog, IAppPreferences preferences, IRemoteUrlHistory remoteUrls,
        string workingDirectory, bool canCreatePullRequest, Func<Task>? openPull,
        Func<string?, Task<PushRepository>>? manageRemotes, Func<string, Task>? loadSshKey,
        Func<string, Task<IReadOnlyList<IGitRef>?>>? readRemoteHeads, Func<Task<bool>> confirmNewBranch)
        : base(host)
    {
        Dialog = dialog;
        _preferences = preferences;
        _remoteUrls = remoteUrls;
        _openPull = openPull;
        _manageRemotes = manageRemotes;
        _loadSshKey = loadSshKey;
        _readRemoteHeads = readRemoteHeads;
        _confirmNewBranch = confirmNewBranch;
        Title = string.Format("{0} ({1})", Host.Translate(PushDialog.PushCaption), workingDirectory);
        ForceWithLeaseTip = Host.Translate(PushDialog.ForceWithLeaseTooltip);
        CanCreatePullRequest = canCreatePullRequest || dialog.HasAzureDevOpsRemote;
        OptionsVisible = preferences.AlwaysShowAdvOpt;
        dialog.RecursiveSubmodules = preferences.RecursiveSubmodules;
        ShowRepository();
        ShowDialogState();
    }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public PushDialog Dialog { get; }

    public string Title { get; }

    public string ForceWithLeaseTip { get; }

    public static IReadOnlyList<string> RecursiveSubmoduleChoices => PushDialog.RecursiveSubmoduleChoices;

    public bool CanCreatePullRequest { get; }

    public bool CanManageRemotes => _manageRemotes is not null;

    public bool CanPull => _openPull is not null;

    /// <summary>
    ///  Upstream's PushOptionsPanel, shown by "Show options" or always (AlwaysShowAdvOpt).
    /// </summary>
    [ObservableProperty]
    public partial bool OptionsVisible { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> Remotes { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<string> Branches { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<string> Tags { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<string> RemoteBranches { get; private set; } = [];

    /// <summary>
    ///  The remote URL history the URL box lists.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> UrlHistory { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<PushBranchRow> MultipleBranches { get; private set; } = [];

    [ObservableProperty]
    public partial string? Remote { get; set; }

    [ObservableProperty]
    public partial bool ToUrl { get; set; }

    [ObservableProperty]
    public partial string? Destination { get; set; }

    /// <summary>
    ///  The tab on show, in upstream's order: branches, tags, multiple branches.
    /// </summary>
    [ObservableProperty]
    public partial int SelectedTab { get; set; }

    [ObservableProperty]
    public partial string? Branch { get; set; }

    [ObservableProperty]
    public partial string? RemoteBranch { get; set; }

    [ObservableProperty]
    public partial bool RemoteBranchEnabled { get; private set; }

    [ObservableProperty]
    public partial string? Tag { get; set; }

    /// <summary>
    ///  Upstream's "Force with lease", which the tag tab's force box shows too (upstream binds the two boxes).
    /// </summary>
    [ObservableProperty]
    public partial bool ForceWithLease { get; set; }

    [ObservableProperty]
    public partial bool Force { get; set; }

    [ObservableProperty]
    public partial bool ReplaceTrackingReference { get; set; }

    [ObservableProperty]
    public partial bool CreatePullRequest { get; set; }

    [ObservableProperty]
    public partial int RecursiveSubmodules { get; set; }

    [ObservableProperty]
    public partial bool ShowLoadSshKey { get; private set; }

    /// <summary>
    ///  Picks a folder to push to, as a file URL; the window sets it, as the picker is the platform's.
    /// </summary>
    public Func<Task<string?>>? PickDestination { get; set; }

    /// <summary>
    ///  Upstream's FormPush loads the remote URL history into the URL box, keeping the typed text. The window calls it
    ///  when it opens.
    /// </summary>
    public Task LoadUrlHistoryAsync() => GuardAsync(async () =>
    {
        IList<GitCommands.UserRepositoryHistory.Repository> history = await _remoteUrls.LoadAsync();
        string? typed = Destination;
        UrlHistory = [.. history.Select(repository => repository.Path)];
        Destination = typed;
        OnPropertyChanged(nameof(Destination));
    });

    // A list box writes null while its items change (also while the window is built); it offers no null choice.
    partial void OnRemoteChanged(string? value)
    {
        if (value is not null)
        {
            Edit(() => Dialog.Remote = value);
        }
    }

    partial void OnToUrlChanged(bool value) => Edit(() => Dialog.ToUrl = value);

    partial void OnBranchChanged(string? value)
    {
        if (value is not null)
        {
            Edit(() => Dialog.Branch = value);
        }
    }

    partial void OnForceWithLeaseChanged(bool value) => Edit(() => Dialog.ForceWithLease = value);

    partial void OnForceChanged(bool value) => Edit(() => Dialog.Force = value);

    partial void OnSelectedTabChanged(int value) => Edit(() => Dialog.Tab = value switch
    {
        1 => PushTab.Tags,
        2 => PushTab.MultipleBranches,
        _ => PushTab.Branches,
    });

    private void Edit(Action change)
    {
        if (_showing)
        {
            return;
        }

        KeepTypedValues();
        change();
        ShowDialogState();
    }

    private void KeepTypedValues()
    {
        Dialog.Destination = Destination?.Trim() ?? "";
        Dialog.RemoteBranch = RemoteBranch?.Trim() ?? "";
        Dialog.Tag = Tag ?? "";
        Dialog.ReplaceTrackingReference = ReplaceTrackingReference;
        Dialog.CreatePullRequest = CreatePullRequest;
        Dialog.RecursiveSubmodules = Math.Max(0, RecursiveSubmodules);
    }

    // The lists' boxes clear their choice when the list changes; the dialog state shows again afterwards.
    private void ShowRepository()
    {
        _showing = true;
        Remotes = Dialog.Remotes;
        Branches = Dialog.Branches;
        Tags = Dialog.Tags;
        _showing = false;
    }

    private void ShowDialogState()
    {
        _showing = true;

        // As upstream, the rows are read when the tab shows and again for another remote.
        if (Dialog.Tab == PushTab.MultipleBranches && _rowsRemote != Dialog.Remote)
        {
            _rowsRemote = Dialog.Remote;
            if (_readRemoteHeads is null)
            {
                Dialog.LoadMultipleBranches();
                MultipleBranches = Dialog.MultipleBranches;
            }
            else
            {
                string remote = Dialog.Remote;
                Run(() => LoadRemoteHeadsAsync(remote));
            }
        }

        ToUrl = Dialog.ToUrl;
        Remote = Dialog.Remote;
        ShowLoadSshKey = _loadSshKey is not null && Dialog.RemoteHasPuttyKey;
        Destination = Dialog.Destination;
        Branch = Dialog.Branch;
        IReadOnlyList<string> remoteBranches = Dialog.RemoteBranches;
        if (!remoteBranches.SequenceEqual(RemoteBranches))
        {
            RemoteBranches = remoteBranches;
        }

        // The editable list clears its text when its items change; the text is put back afterwards.
        RemoteBranch = Dialog.RemoteBranch;
        OnPropertyChanged(nameof(RemoteBranch));
        RemoteBranchEnabled = Dialog.Branch != PushDialog.AllRefs;
        ForceWithLease = Dialog.ForceWithLease;
        Force = Dialog.Force;
        ReplaceTrackingReference = Dialog.ReplaceTrackingReference;
        CreatePullRequest = Dialog.CreatePullRequest;
        RecursiveSubmodules = Dialog.RecursiveSubmodules;
        Tag = Dialog.Tag.Length > 0 ? Dialog.Tag : null;
        SelectedTab = Dialog.Tab switch
        {
            PushTab.Tags => 1,
            PushTab.MultipleBranches => 2,
            _ => 0,
        };
        _showing = false;
    }

    // Upstream's LoadMultiBranchViewData with GetRemoteBranchesDirectlyFromRemote: the remote's branches as git's
    // ls-remote lists them; after an error the rows stay as they were, as upstream's.
    private async Task LoadRemoteHeadsAsync(string remote)
    {
        if (_readRemoteHeads is not null && await _readRemoteHeads(remote) is { } heads && _rowsRemote == remote)
        {
            Dialog.LoadMultipleBranches(heads);
            MultipleBranches = Dialog.MultipleBranches;
        }
    }

    [RelayCommand]
    private void ShowOptions() => OptionsVisible = true;

    [RelayCommand]
    private void UnselectAll() => Dialog.SelectAll(false);

    [RelayCommand]
    private void SelectAll() => Dialog.SelectAll(true);

    [RelayCommand]
    private void SelectTracked() => Dialog.SelectTracked();

    [RelayCommand]
    private Task PullAsync() => GuardAsync(() => _openPull?.Invoke() ?? Task.CompletedTask);

    [RelayCommand]
    private Task LoadSshKeyAsync() => GuardAsync(() => _loadSshKey?.Invoke(Dialog.Remote) ?? Task.CompletedTask);

    [RelayCommand]
    private Task BrowseDestinationAsync() => GuardAsync(async () =>
    {
        if (PickDestination is not null && await PickDestination() is { } url)
        {
            Destination = url;
        }
    });

    [RelayCommand]
    private Task ManageRemotesAsync() => GuardAsync(() => ManageRemotesAsync(Dialog.Remote));

    // Upstream's OpenRemotesDialogAndRefreshList.
    private async Task ManageRemotesAsync(string? remote)
    {
        if (_manageRemotes is null)
        {
            return;
        }

        KeepTypedValues();
        Dialog.UpdateRepository(await _manageRemotes(remote), remote);
        ShowRepository();
        _rowsRemote = null;
        ShowDialogState();
    }

    // Upstream's PushChanges, up to running the push.
    [RelayCommand]
    private Task PushAsync() => GuardAsync(async () =>
    {
        KeepTypedValues();

        // Upstream's CheckIfRemoteExist: without a remote, the remotes window is offered first.
        if (Dialog.Remotes.Count == 0 && _manageRemotes is not null && !Dialog.ToUrl)
        {
            if (!await Host.ConfirmAsync(Host.Translate(PushDialog.ConfigureRemote),
                    Host.Translate(PushDialog.ErrorPushToRemoteCaption)))
            {
                return;
            }

            await ManageRemotesAsync(null);
            if (Dialog.Remotes.Count == 0)
            {
                return;
            }
        }

        if (Dialog.ValidationError() is { } error)
        {
            await Host.ShowMessageAsync(Host.Translate(error),
                Host.Translate(error == PushDialog.ConfigureRemote ? PushDialog.ErrorPushToRemoteCaption : "Error"));
            return;
        }

        if (Dialog.IsNewBranchForRemote && !await _confirmNewBranch())
        {
            return;
        }

        // Upstream adds the URL pushed to to the remote URL history.
        if (Dialog.ToUrl)
        {
            await _remoteUrls.AddAsMostRecentAsync(Dialog.Destination);
        }

        _preferences.RecursiveSubmodules = Dialog.RecursiveSubmodules;
        bool track = false;
        if (Dialog.TrackingQuestion is { } question)
        {
            track = true;
            if (_preferences.Asks(Confirmation.AddTrackingRef))
            {
                if (await Host.AskAsync(Host.Translate(question), Host.Translate(PushDialog.PushCaption)) is not { } answer)
                {
                    return;
                }

                track = answer;
            }
        }

        if (Dialog.Tab == PushTab.Branches && Dialog.Force)
        {
            bool? forceWithLease = await Host.AskAsync(Host.Translate(PushDialog.UseForceWithLeaseInstead), "Question");
            if (forceWithLease is null)
            {
                return;
            }

            if (forceWithLease == true)
            {
                Dialog.ForceWithLease = true;
                ShowDialogState();
            }
        }

        Host.Close(Dialog.Request(track));
    });
}
