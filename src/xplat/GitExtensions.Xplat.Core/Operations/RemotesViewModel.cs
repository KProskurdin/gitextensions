using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitCommands.Config;
using GitCommands.Remotes;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using UserRepository = GitCommands.UserRepositoryHistory.Repository;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormRemotes</c> (rules in <see cref="RemotesDialog"/>): the remotes (active and
///  inactive) with the details of the selected one, and the default pull behavior of the local branches. A saved remote
///  is fetched, as upstream does.
/// </summary>
public sealed partial class RemotesViewModel : DialogViewModel
{
    /// <summary>
    ///  The "Default pull behavior" tab.
    /// </summary>
    public const int PullBehaviorTab = 1;

    // Upstream's ColorButton text without a color.
    private const string SetColorText = "Set &color";

    private readonly GitModule _module;
    private readonly IAppPreferences _preferences;
    private readonly IRemoteUrlHistory _remoteUrls;
    private readonly Func<string, Task>? _fetchRemote;
    private readonly string? _preselectRemote;
    private readonly string? _preselectLocal;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IList<UserRepository> _history = [];
    private bool _showing;

    /// <param name="module">The repository.</param>
    /// <param name="dialog">The dialog state over the repository's remotes.</param>
    /// <param name="preferences">Upstream's advanced options and the branch name normalisation.</param>
    /// <param name="remoteUrls">The remote URL history the URL boxes list and a saved remote's URLs are added to.</param>
    /// <param name="fetchRemote">Fetches a saved remote, as upstream pulls with the fetch action after a save.</param>
    /// <param name="preselectRemote">Upstream's <c>PreselectRemoteOnLoad</c>.</param>
    /// <param name="preselectLocal">Upstream's <c>PreselectLocalOnLoad</c>: opens the pull behavior tab on this
    ///  branch.</param>
    public RemotesViewModel(IDialogHost host, GitModule module, RemotesDialog dialog, IAppPreferences preferences,
        IRemoteUrlHistory remoteUrls, Func<string, Task>? fetchRemote, string? preselectRemote, string? preselectLocal)
        : base(host)
    {
        if (preselectRemote is not null && preselectLocal is not null)
        {
            throw new ArgumentException("Only one option allowed: either preselectRemote or preselectLocal");
        }

        _module = module;
        Dialog = dialog;
        _preferences = preferences;
        _remoteUrls = remoteUrls;
        _fetchRemote = fetchRemote;
        _preselectRemote = preselectRemote;
        _preselectLocal = preselectLocal;
        ShowAdvanced = preferences.AlwaysShowAdvOpt;
        NewTip = Host.Translate(RemotesDialog.NewTooltip);
        DeleteTip = Host.Translate(RemotesDialog.DeleteTooltip);
        ShowDetails();
    }

    /// <summary>
    ///  Raised when New was pressed: the window focuses the name box.
    /// </summary>
    public event EventHandler? NameRequired;

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public RemotesDialog Dialog { get; }

    /// <summary>
    ///  Completes when the remotes and branches shown first are read, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Upstream shows the prefix and the color only with its advanced options (AlwaysShowAdvOpt).
    /// </summary>
    public bool ShowAdvanced { get; }

    public string NewTip { get; }

    public string DeleteTip { get; }

    /// <summary>
    ///  Picks a folder for a URL box; the window sets it, as the picker is the platform's.
    /// </summary>
    public Func<Task<string?>>? PickFolder { get; set; }

    /// <summary>
    ///  Picks a PuTTY key file; the window sets it.
    /// </summary>
    public Func<Task<string?>>? PickPuttyKey { get; set; }

    [ObservableProperty]
    public partial int SelectedTab { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<RemoteListRow> RemoteRows { get; private set; } = [];

    [ObservableProperty]
    public partial RemoteListRow? SelectedRemoteRow { get; set; }

    [ObservableProperty]
    public partial string DetailsHeader { get; private set; } = "";

    [ObservableProperty]
    public partial string UrlLabel { get; private set; } = "";

    [ObservableProperty]
    public partial string? Name { get; set; }

    [ObservableProperty]
    public partial string? Url { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> UrlChoices { get; private set; } = [];

    [ObservableProperty]
    public partial string? PushUrl { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> PushUrlChoices { get; private set; } = [];

    [ObservableProperty]
    public partial bool SeparatePushUrl { get; set; }

    [ObservableProperty]
    public partial string? PuttySshKey { get; set; }

    [ObservableProperty]
    public partial string? Prefix { get; set; }

    /// <summary>
    ///  The remote's color as HTML (#RRGGBB); null for the default.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasColor), nameof(ColorButtonText), nameof(ColorHtml))]
    public partial string? Color { get; private set; }

    public bool HasColor => Color is not null;

    public string ColorButtonText => Color is null ? Host.Translate(SetColorText).Replace('&', '_') : "";

    public string ColorHtml => Color ?? "";

    [ObservableProperty]
    public partial bool CanDelete { get; private set; }

    /// <summary>
    ///  The selected remote is inactive: the toggle button activates it.
    /// </summary>
    [ObservableProperty]
    public partial bool SelectedIsInactive { get; private set; }

    [ObservableProperty]
    public partial string ToggleTip { get; private set; } = "";

    /// <summary>
    ///  As upstream, an inactive remote's details are shown but cannot be edited.
    /// </summary>
    [ObservableProperty]
    public partial bool IsEditable { get; private set; } = true;

    [ObservableProperty]
    public partial bool CanSave { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<PullBehaviorRow> BranchRows { get; private set; } = [];

    [ObservableProperty]
    public partial PullBehaviorRow? SelectedBranchRow { get; set; }

    [ObservableProperty]
    public partial string? LocalBranch { get; private set; }

    /// <summary>
    ///  The remotes a branch can pull from, an empty choice first.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> RemoteChoices { get; private set; } = [];

    [ObservableProperty]
    public partial string? BranchRemote { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> MergeWithChoices { get; private set; } = [];

    [ObservableProperty]
    public partial string? MergeWith { get; set; }

    /// <summary>
    ///  Reads the remotes and the local branches; the window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        try
        {
            _history = await _remoteUrls.LoadAsync();
            await Task.Run(() => Dialog.Load(_preselectRemote));
            ShowRemotes();
            await LoadPullBehaviorAsync(_preselectLocal);
            if (_preselectLocal is not null && Dialog.Remotes.Count > 0)
            {
                SelectedTab = PullBehaviorTab;
            }
        }
        finally
        {
            _loaded.TrySetResult();
        }
    });

    /// <summary>
    ///  Upstream's name proposed from the URL when the name box gets the focus; true when the box was filled.
    /// </summary>
    public bool ProposeName()
    {
        KeepTypedValues();
        if (Dialog.NameFromUrl() is not { } owner)
        {
            return false;
        }

        Name = owner;
        return true;
    }

    /// <summary>
    ///  Upstream's <c>FillWithSomeGeneratedRemoteUrls</c>, when a URL box gets the focus.
    /// </summary>
    public void ProposeUrls(bool pushUrl)
    {
        KeepTypedValues();
        string text = (pushUrl ? PushUrl : Url) ?? "";
        IReadOnlyList<string> proposed = Dialog.ProposedUrls(pushUrl, text, [.. _history.Select(entry => entry.Path)],
            out bool clearText);
        if (proposed.Count == 0)
        {
            return;
        }

        // The editable list clears its text when its items change; the text is put back afterwards.
        string shown = clearText ? "" : text;
        if (pushUrl)
        {
            PushUrlChoices = proposed;
            PushUrl = shown;
            OnPropertyChanged(nameof(PushUrl));
        }
        else
        {
            UrlChoices = proposed;
            Url = shown;
            OnPropertyChanged(nameof(Url));
        }
    }

    /// <summary>
    ///  The prefix normalised as a branch name part, when its box is left.
    /// </summary>
    public void NormalisePrefix() => Prefix = BranchNames.NormalisePrefix(Prefix ?? "", _preferences.AutoNormaliseSymbol);

    /// <summary>
    ///  As upstream, which writes a branch's remote when its box is left; Save only reads them again.
    /// </summary>
    public void StoreBranchRemote()
    {
        string remote = BranchRemote ?? "";
        Run(() => SetOnSelectedHeadAsync(head => head.TrackingRemote = remote));
    }

    /// <summary>
    ///  As upstream, which writes a branch's merge branch when its box is left.
    /// </summary>
    public void StoreMergeWith()
    {
        string mergeWith = MergeWith ?? "";
        Run(() => SetOnSelectedHeadAsync(head => head.MergeWith = mergeWith));
    }

    /// <summary>
    ///  The remote branches the selected branch can merge with, when the list opens.
    /// </summary>
    public void FillMergeWithChoices() => Run(async () =>
    {
        if (SelectedBranchRow?.Head is not { } head)
        {
            return;
        }

        string remote = BranchRemote ?? "";
        string text = MergeWith ?? "";
        IReadOnlyList<string> choices = await Task.Run(() => RemotesDialog.MergeWithChoices(head, remote,
            remote.Length == 0 ? null : _module.GetSetting(string.Format(SettingKeyString.RemoteUrl, remote)),
            _module.GetRefs(RefsFilter.Remotes)));
        MergeWithChoices = choices;
        MergeWith = text;
        OnPropertyChanged(nameof(MergeWith));
    });

    partial void OnSelectedRemoteRowChanged(RemoteListRow? value)
    {
        if (value?.Remote is { } remote)
        {
            Edit(() => Dialog.Select(remote));
        }
    }

    partial void OnNameChanged(string? value)
    {
        if (!_showing)
        {
            Dialog.Name = value ?? "";
            CanSave = Dialog.CanSave;
        }
    }

    partial void OnSeparatePushUrlChanged(bool value) => Edit(() => Dialog.SeparatePushUrl = value);

    partial void OnSelectedBranchRowChanged(PullBehaviorRow? value)
    {
        if (value is null)
        {
            return;
        }

        LocalBranch = value.Head.Name;
        BranchRemote = Dialog.Remotes
            .FirstOrDefault(remote => StringComparer.OrdinalIgnoreCase.Equals(remote.Name, value.TrackingRemote))?.Name ?? "";
        MergeWith = value.MergeWith;
    }

    private void Edit(Action change)
    {
        if (_showing)
        {
            return;
        }

        KeepTypedValues();
        change();
        ShowDetails();
    }

    private void KeepTypedValues()
    {
        Dialog.Name = Name ?? "";
        Dialog.Url = Url ?? "";
        Dialog.PushUrl = PushUrl ?? "";
        Dialog.PuttySshKey = PuttySshKey ?? "";
        Dialog.Prefix = Prefix ?? "";
    }

    private void ShowRemotes()
    {
        _showing = true;
        List<RemoteListRow> rows = [];
        AddGroup(RemotesDialog.ActiveGroup, Dialog.ActiveRemotes);
        AddGroup(RemotesDialog.InactiveGroup, Dialog.InactiveRemotes);
        RemoteRows = rows;
        SelectedRemoteRow = rows.FirstOrDefault(row => row.Remote is not null && row.Remote == Dialog.Selected);
        UrlChoices = [.. _history.Select(entry => entry.Path)];
        PushUrlChoices = UrlChoices;
        _showing = false;
        ShowDetails();
        return;

        void AddGroup(string header, IEnumerable<ConfigFileRemote> remotes)
        {
            List<ConfigFileRemote> group = [.. remotes];
            if (group.Count > 0)
            {
                rows.Add(new RemoteListRow(Host.Translate(header), null));
                rows.AddRange(group.Select(remote => new RemoteListRow(remote.Name ?? "", remote)));
            }
        }
    }

    private void ShowDetails()
    {
        _showing = true;
        DetailsHeader = Host.Translate(Dialog.Header);
        UrlLabel = Host.Translate(Dialog.UrlLabel).Replace('&', '_');
        Name = Dialog.Name;
        Url = Dialog.Url;
        PushUrl = Dialog.PushUrl;
        SeparatePushUrl = Dialog.SeparatePushUrl;
        PuttySshKey = Dialog.PuttySshKey;
        Prefix = Dialog.Prefix;
        Color = Dialog.Color;
        CanDelete = Dialog.CanDelete;
        SelectedIsInactive = Dialog.Selected?.Disabled == true;
        ToggleTip = Host.Translate(SelectedIsInactive ? RemotesDialog.ActivateTooltip : RemotesDialog.DeactivateTooltip)
            .Trim();
        IsEditable = Dialog.IsEditable;
        CanSave = Dialog.IsEditable && Dialog.CanSave;
        _showing = false;
    }

    // Upstream's InitialiseTabDefaultPullBehaviors: the local branches by name, with their remote and merge branch.
    private async Task LoadPullBehaviorAsync(string? preselectLocal)
    {
        List<PullBehaviorRow> rows = await Task.Run(() => _module.GetRefs(RefsFilter.Heads)
            .OrderBy(head => head.LocalName)
            .Select(head => new PullBehaviorRow(head, head.LocalName, head.TrackingRemote, head.MergeWith))
            .ToList());
        RemoteChoices = ["", .. Dialog.Remotes.Select(remote => remote.Name ?? "")];
        BranchRows = rows;
        SelectedBranchRow = rows.FirstOrDefault(row => row.LocalName == preselectLocal) ?? rows.FirstOrDefault();
    }

    private async Task SetOnSelectedHeadAsync(Action<IGitRef> set)
    {
        if (SelectedBranchRow?.Head is { } head)
        {
            await Task.Run(() => set(head));
        }
    }

    private async Task ReloadAsync(string? selected)
    {
        await Task.Run(() => Dialog.Load(selected));
        ShowRemotes();
        await LoadPullBehaviorAsync(SelectedBranchRow?.LocalName);
    }

    [RelayCommand]
    private void New()
    {
        Edit(() => Dialog.Select(null));
        NameRequired?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private Task ToggleStateAsync() => GuardAsync(async () =>
    {
        await Task.Run(Dialog.ToggleState);
        ShowRemotes();
    });

    [RelayCommand]
    private void ChooseColor(string? html)
    {
        Dialog.Color = html;
        Color = html;
    }

    [RelayCommand]
    private Task BrowseUrlAsync() => GuardAsync(async () =>
    {
        if (PickFolder is not null && await PickFolder() is { } path)
        {
            Url = path;
        }
    });

    [RelayCommand]
    private Task BrowsePushUrlAsync() => GuardAsync(async () =>
    {
        if (PickFolder is not null && await PickFolder() is { } path)
        {
            PushUrl = path;
        }
    });

    [RelayCommand]
    private Task BrowsePuttyKeyAsync() => GuardAsync(async () =>
    {
        if (PickPuttyKey is not null && await PickPuttyKey() is { } path)
        {
            PuttySshKey = path;
        }
    });

    // Upstream's LoadSshKeyClick: Pageant with the key in the box.
    [RelayCommand]
    private Task LoadSshKeyAsync() => GuardAsync(async () =>
    {
        string key = PuttySshKey ?? "";
        if (await Task.Run(() => Putty.StartPageantIfConfigured(key, Host.Translate)) is { } error)
        {
            Host.ShowError(new InvalidOperationException(error));
        }
    });

    // Upstream's TestConnectionClick: plink to the URL in the box, in its own console window.
    [RelayCommand]
    private Task TestConnectionAsync() => GuardAsync(async () =>
    {
        if (await Putty.TestConnectionAsync(Url ?? "", Host.Translate) is { } error)
        {
            Host.ShowError(new InvalidOperationException(error));
        }
    });

    [RelayCommand]
    private Task SaveAsync() => GuardAsync(async () =>
    {
        KeepTypedValues();
        ConfigFileRemote? before = Dialog.Selected;
        string url = Dialog.Url.Trim();
        string pushUrl = Dialog.PushUrl.Trim();
        RemoteSaveOutcome outcome = await Task.Run(Dialog.Save);
        try
        {
            if (outcome.Message is { } message)
            {
                await Host.ShowMessageAsync(message, Host.Translate(RemotesDialog.GitMessage));
                return;
            }

            if (outcome.SavedName is not { } name)
            {
                return;
            }

            Dialog.UpdateUrlHistory(_history, before, url, Dialog.SeparatePushUrl ? pushUrl : null);
            await _remoteUrls.SaveAsync(_history);
            if (_fetchRemote is not null)
            {
                await _fetchRemote(name);
            }
        }
        finally
        {
            // As upstream, the list is read again whatever happened, with the remote typed selected.
            await ReloadAsync(Dialog.Name.Trim());
        }
    });

    [RelayCommand]
    private Task DeleteAsync() => GuardAsync(async () =>
    {
        if (Dialog.Selected is null
            || !await Host.ConfirmAsync(Host.Translate(RemotesDialog.QuestionDeleteRemote),
                Host.Translate(RemotesDialog.QuestionDeleteRemoteCaption), defaultIsNo: true))
        {
            return;
        }

        string output = await Task.Run(Dialog.Delete);
        if (!string.IsNullOrEmpty(output))
        {
            await Host.ShowMessageAsync(output, Host.Translate(RemotesDialog.GitMessage));
        }

        await ReloadAsync(null);
    });

    [RelayCommand]
    private Task SavePullBehaviorAsync() => GuardAsync(() => LoadPullBehaviorAsync(SelectedBranchRow?.LocalName));
}

/// <summary>
///  A row of the remotes list: a group's header ("Active", "Inactive") or a remote.
/// </summary>
public sealed record RemoteListRow(string Text, ConfigFileRemote? Remote)
{
    public bool IsHeader => Remote is null;

    public bool IsSelectable => Remote is not null;

    public bool IsInactive => Remote?.Disabled == true;
}

/// <summary>
///  A row of the "Default pull behavior" tab: a local branch, the remote it pulls from and the branch it merges.
/// </summary>
public sealed record PullBehaviorRow(IGitRef Head, string LocalName, string TrackingRemote, string MergeWith);
