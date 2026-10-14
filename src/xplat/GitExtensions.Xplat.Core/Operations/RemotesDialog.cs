using GitCommands.Remotes;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Extensibility.Git;
using UserRepository = GitCommands.UserRepositoryHistory.Repository;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>FormRemotes</c> without its window: the active and inactive remotes, the details of the selected one (or
///  of a new one), and saving, deleting, activating and deactivating them through upstream's
///  <see cref="IConfigFileRemoteSettingsManager"/>; and, for the "Default pull behavior" tab, the remote branches a local
///  branch can merge with.
/// </summary>
public sealed class RemotesDialog
{
    // Upstream's texts.
    public const string GitMessage = "Message";
    public const string QuestionDeleteRemote = "Are you sure you want to delete this remote?";
    public const string QuestionDeleteRemoteCaption = "Delete";
    public const string SshKeyOpenFilter = "Private key (*.ppk)";
    public const string SshKeyOpenCaption = "Select ssh key file";
    public const string LabelUrlAsFetch = "Fetch &Url";
    public const string LabelUrlAsFetchPush = "&Url";
    public const string HeaderNew = "Create New Remote";
    public const string HeaderEdit = "Edit Remote Details";
    public const string DeleteTooltip = "Delete the selected remote";
    public const string NewTooltip = "Add a new remote";
    public const string ActivateTooltip = "Activate the selected remote";
    public const string ActiveGroup = "Active";
    public const string InactiveGroup = "Inactive";
    public const string EnabledRemoteAlreadyExists = "An active remote named \"{0}\" already exists.";
    public const string DisabledRemoteAlreadyExists = "An inactive remote named \"{0}\" already exists.";

    // Upstream's placeholder for the new remote's name in a proposed URL.
    private const string ToReplace = "TO_REPLACE";

    public static readonly string DeactivateTooltip =
        $"Deactivate the selected remote.{Environment.NewLine}Inactive remote is completely invisible to git.";

    private static readonly string[] _genericRemoteNames = ["origin", "upstream", "fork", "remote", "internal"];

    private readonly IConfigFileRemoteSettingsManager _manager;
    private readonly IReadOnlyList<string> _customGenericRemoteNames;
    private ConfigFileRemote? _selected;

    /// <param name="customGenericRemoteNames">Upstream's <c>CustomGenericRemoteNames</c>.</param>
    public RemotesDialog(IConfigFileRemoteSettingsManager manager, IEnumerable<string>? customGenericRemoteNames = null)
    {
        _manager = manager;
        _customGenericRemoteNames = [.. customGenericRemoteNames ?? []];
    }

    /// <summary>
    ///  The remotes in .git/config, the inactive ones included, as upstream lists them.
    /// </summary>
    public IReadOnlyList<ConfigFileRemote> Remotes { get; private set; } = [];

    public IEnumerable<ConfigFileRemote> ActiveRemotes => Remotes.Where(remote => !remote.Disabled);

    public IEnumerable<ConfigFileRemote> InactiveRemotes => Remotes.Where(remote => remote.Disabled);

    /// <summary>
    ///  The remote whose details are shown, or null for a new remote.
    /// </summary>
    public ConfigFileRemote? Selected => _selected;

    public string Name { get; set; } = "";

    public string Url { get; set; } = "";

    public string PushUrl { get; set; } = "";

    /// <summary>
    ///  Upstream's "Separate Push Url".
    /// </summary>
    public bool SeparatePushUrl { get; set; }

    public string PuttySshKey { get; set; } = "";

    public string Prefix { get; set; } = "";

    /// <summary>
    ///  The remote's color as HTML (<c>remote.&lt;name&gt;.color</c>), or null for the theme's color.
    /// </summary>
    public string? Color { get; set; }

    public string Header => _selected is null ? HeaderNew : HeaderEdit;

    public string UrlLabel => SeparatePushUrl ? LabelUrlAsFetch : LabelUrlAsFetchPush;

    public bool CanSave => Name.Trim().Length > 0;

    public bool CanDelete => _selected is not null;

    /// <summary>
    ///  False for an inactive remote, whose details upstream shows but does not let edit.
    /// </summary>
    public bool IsEditable => _selected?.Disabled != true;

    /// <summary>
    ///  Reads the remotes and selects <paramref name="preselect"/>, else the first active remote, else the first inactive
    ///  one, as upstream's <c>BindRemotes</c>; with no remote, a new one.
    /// </summary>
    public void Load(string? preselect = null)
    {
        Remotes = [.. _manager.LoadRemotes(true)];
        Select(Remotes.FirstOrDefault(remote => !string.IsNullOrEmpty(preselect) && remote.Name == preselect)
               ?? ActiveRemotes.FirstOrDefault()
               ?? InactiveRemotes.FirstOrDefault());
    }

    /// <summary>
    ///  Shows the details of <paramref name="remote"/>, or empty details for a new remote (upstream's
    ///  <c>Remotes_SelectedIndexChanged</c>).
    /// </summary>
    public void Select(ConfigFileRemote? remote)
    {
        _selected = remote;
        Name = remote?.Name ?? "";
        Url = remote?.Url ?? "";
        PushUrl = remote?.PushUrl ?? "";
        SeparatePushUrl = !string.IsNullOrEmpty(remote?.PushUrl);
        PuttySshKey = remote?.PuttySshKey ?? "";
        Prefix = remote?.Prefix ?? "";
        Color = string.IsNullOrWhiteSpace(remote?.Color) ? null : remote.Color;
    }

    /// <summary>
    ///  Saves the details as upstream's <c>SaveClick</c>: a new remote is added unless one of that name exists, an existing
    ///  one is renamed and updated. <see cref="RemoteSaveOutcome.Message"/> is what upstream shows; after a save upstream
    ///  fetches the remote and records its URLs in the remote history (<see cref="UpdateUrlHistory"/>).
    /// </summary>
    public RemoteSaveOutcome Save()
    {
        if (!CanSave)
        {
            return new RemoteSaveOutcome(null, null);
        }

        string name = Name.Trim();
        string url = Url.Trim();
        string pushUrl = PushUrl.Trim();
        if ((pushUrl.Length == 0 && SeparatePushUrl) ||
            (pushUrl.Length > 0 && pushUrl.Equals(url, StringComparison.OrdinalIgnoreCase)))
        {
            SeparatePushUrl = false;
        }

        if (_selected is null)
        {
            if (_manager.EnabledRemoteExists(name))
            {
                return new RemoteSaveOutcome(string.Format(EnabledRemoteAlreadyExists, name), null);
            }

            if (_manager.DisabledRemoteExists(name))
            {
                return new RemoteSaveOutcome(string.Format(DisabledRemoteAlreadyExists, name), null);
            }
        }

        ConfigFileRemoteSaveResult result = _manager.SaveRemote(_selected, name, url, SeparatePushUrl ? pushUrl : null,
            PuttySshKey, Color, Prefix);
        return string.IsNullOrEmpty(result.UserMessage)
            ? new RemoteSaveOutcome(null, name)
            : new RemoteSaveOutcome(result.UserMessage, null);
    }

    /// <summary>
    ///  Upstream's <c>FormRemotesController.RemoteUpdate</c> for the URLs just saved: a changed URL replaces the old one in
    ///  the remote history, a new one goes first.
    /// </summary>
    public void UpdateUrlHistory(IList<UserRepository> history, ConfigFileRemote? before, string url, string? pushUrl)
    {
        Update(history, before?.Url, url);
        if (pushUrl is not null)
        {
            Update(history, before?.PushUrl, pushUrl);
        }

        static void Update(IList<UserRepository> remotes, string? oldUrl, string? newUrl)
        {
            if (string.IsNullOrWhiteSpace(newUrl))
            {
                return;
            }

            if (!string.Equals(oldUrl, newUrl, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(oldUrl) && remotes.FirstOrDefault(r => r.Path == oldUrl) is { } old)
            {
                remotes.Remove(old);
            }

            if (remotes.All(r => r.Path != newUrl))
            {
                remotes.Insert(0, new UserRepository(newUrl));
            }
        }
    }

    /// <summary>
    ///  Removes the selected remote; returns git's output, which upstream shows when there is any.
    /// </summary>
    public string Delete() => _selected is null ? "" : _manager.RemoveRemote(_selected);

    /// <summary>
    ///  Activates an inactive remote or deactivates an active one, and reads the remotes again with it selected.
    /// </summary>
    public void ToggleState()
    {
        if (_selected?.Name is not { } name)
        {
            return;
        }

        _manager.ToggleRemoteState(name, !_selected.Disabled);
        Load(name);
    }

    /// <summary>
    ///  Upstream's <c>FillWithSomeGeneratedRemoteUrls</c>: URLs for the remote being named, made from the other remotes'
    ///  URLs (their name, or the hosting owner, replaced by the new name), before the remote history; empty when none is
    ///  new. <paramref name="clearText"/> is true when upstream empties the box rather than picking one.
    /// </summary>
    public IReadOnlyList<string> ProposedUrls(bool pushUrl, string currentText, IReadOnlyList<string> history,
        out bool clearText)
    {
        clearText = false;
        string remoteName = Name;
        bool fillEmptyUrl = true;
        if (string.IsNullOrWhiteSpace(Name) || _genericRemoteNames.Contains(Name) ||
            _customGenericRemoteNames.Contains(Name))
        {
            remoteName = ToReplace;
            fillEmptyUrl = false;
        }

        HashSet<string> candidates = [];
        GitHostingRemoteParser parser = new();
        foreach (ConfigFileRemote remote in Remotes)
        {
            string? url = pushUrl ? remote.PushUrl : remote.Url;
            if (string.IsNullOrEmpty(url))
            {
                continue;
            }

            if (url.Contains(remote.Name!))
            {
                candidates.Add(url.Replace($"{remote.Name}/", $"{remoteName}/"));
            }

            if (parser.TryExtractGitHostingDataFromRemoteUrl(remote.Url!, out _, out string? owner, out _))
            {
                candidates.Add(url.Replace($"{owner}/", $"{remoteName}/"));
            }
        }

        List<string> proposed = [.. history];
        bool added = false;
        foreach (string url in candidates.Where(url => !proposed.Contains(url)))
        {
            added = true;
            proposed.Insert(0, url);
        }

        if (!added)
        {
            return [];
        }

        if (currentText.Length > 0)
        {
            proposed.Insert(0, currentText);
        }
        else if (!fillEmptyUrl || candidates.Count > 1)
        {
            clearText = true;
        }

        return proposed;
    }

    /// <summary>
    ///  Upstream's <c>RemoteName_Enter</c>: the hosting owner of the URL typed, as a name for a remote that has none yet.
    /// </summary>
    public string? NameFromUrl()
        => Name.Length == 0 && Url.Length > 0 &&
           new GitHostingRemoteParser().TryExtractGitHostingDataFromRemoteUrl(Url, out _, out string? owner, out _)
            ? owner
            : null;

    /// <summary>
    ///  Upstream's <c>DefaultMergeWithComboDropDown</c>: an empty choice, and when the branch tracks a remote and the chosen
    ///  remote has a URL, the remote branches whose name contains the chosen remote's.
    /// </summary>
    public static IReadOnlyList<string> MergeWithChoices(IGitRef head, string remote, string? remoteUrl,
        IEnumerable<IGitRef> remoteBranches)
    {
        remote = remote.Trim();
        if (string.IsNullOrEmpty(head.TrackingRemote) || remote.Length == 0 || string.IsNullOrEmpty(remoteUrl))
        {
            return [""];
        }

        return
        [
            "", .. remoteBranches
                .Where(branch => branch.Name.Contains(remote, StringComparison.OrdinalIgnoreCase))
                .Select(branch => branch.LocalName)
        ];
    }
}

/// <summary>
///  The outcome of <see cref="RemotesDialog.Save"/>: the message upstream shows (null for none), and the saved remote's
///  name (null when nothing was saved).
/// </summary>
public sealed record RemoteSaveOutcome(string? Message, string? SavedName);