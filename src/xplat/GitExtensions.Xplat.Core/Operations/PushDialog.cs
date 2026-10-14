using System.ComponentModel;
using GitCommands;
using GitCommands.Git;
using GitCommands.Remotes;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The tab of upstream's push dialog.
/// </summary>
public enum PushTab
{
    Branches,
    Tags,
    MultipleBranches,
}

/// <summary>
///  A row of the "Push multiple branches" tab: a local branch and its branch on the remote (or a remote branch the
///  repository has no local branch for), how they compare, and what to do with it.
/// </summary>
public sealed class PushBranchRow(string local, string remote, string aheadBehind) : INotifyPropertyChanged
{
    private bool _push;
    private bool _force;
    private bool _delete;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Local { get; } = local;

    public string Remote { get; } = remote;

    /// <summary>
    ///  Upstream's "Ahead/Behind": the counts against the tracked branch, "=" or "&lt;&gt;" against the remote branch of the
    ///  same name, or empty.
    /// </summary>
    public string AheadBehind { get; } = aheadBehind;

    // As upstream's BranchTable_ColumnChanged, the three choices exclude each other.
    public bool Push
    {
        get => _push;
        set => Choose(ref _push, value);
    }

    public bool Force
    {
        get => _force;
        set => Choose(ref _force, value);
    }

    public bool Delete
    {
        get => _delete;
        set => Choose(ref _delete, value);
    }

    private void Choose(ref bool field, bool value)
    {
        if (value)
        {
            _push = _force = _delete = false;
        }

        field = value;
        foreach (string name in (string[])[nameof(Push), nameof(Force), nameof(Delete)])
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

/// <summary>
///  What upstream's push dialog reads from the repository when it opens.
/// </summary>
public sealed record PushRepository(
    string CurrentBranch,
    IReadOnlyList<ConfigFileRemote> Remotes,
    IReadOnlyList<IGitRef> LocalBranches,
    IReadOnlyList<IGitRef> RemoteBranches,
    IReadOnlyList<string> Tags,
    string? CurrentBranchRemote,
    IReadOnlyDictionary<string, AheadBehindData>? AheadBehind,
    bool IsBare,
    string? AutoSetupMerge,
    Func<ConfigFileRemote, string, string?> DefaultPushRemote)
{
    /// <summary>
    ///  Reads the repository as upstream's <c>FormPush</c> does: its remotes, branches, tags and the current branch.
    /// </summary>
    public static Task<PushRepository> LoadAsync(string repositoryPath)
        => Task.Run(() =>
        {
            GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath);
            ConfigFileRemoteSettingsManager remotes = new(() => module);
            IReadOnlyList<IGitRef> refs = module.GetRefs(RefsFilter.Heads | RefsFilter.Remotes);
            string current = module.GetSelectedBranch();

            // A branch reads its tracking settings when first asked; here, off the UI thread.
            foreach (IGitRef head in refs.Where(gitRef => gitRef.IsHead))
            {
                _ = head.TrackingRemote;
                _ = head.MergeWith;
            }

            return new PushRepository(
                DetachedHeadParser.IsDetachedHead(current) ? "" : current,
                [.. remotes.LoadRemotes(false)],
                [.. refs.Where(gitRef => gitRef.IsHead)],
                [.. refs.Where(gitRef => gitRef.IsRemote)],
                [.. module.GetRefs(RefsFilter.Tags).Select(tag => tag.Name)],
                module.GetSetting($"branch.{current}.remote"),
                new AheadBehindDataProvider(() => module.GitExecutable).GetData(),
                module.IsBareRepository(),
                module.GetEffectiveSetting("branch.autosetupmerge"),
                remotes.GetDefaultPushRemote);
        });
}

/// <summary>
///  Upstream's <c>FormPush</c> without its window: the remote and branch it starts with, the remote branch it proposes,
///  the lists, the force options, the checks before pushing, and the request it runs.
/// </summary>
public sealed class PushDialog
{
    public const string AllRefs = "[ All ]";
    public const string HeadText = "HEAD";

    // Upstream's texts.
    public const string PushCaption = "Push";
    public const string PushToCaption = "Push to {0}";
    public const string NoCurrentBranch = "No branch is selected, cannot push.";
    public const string SelectDestinationDirectory = "Please select a destination directory";
    public const string ErrorPushToRemoteCaption = "Push to remote";
    public const string SelectTag = "You need to select a tag to push or select \"Push all tags\".";

    public const string UpdateTrackingReference =
        "The branch {0} does not have a tracking reference. Do you want to add a tracking reference to {1}?";

    public const string UseForceWithLeaseInstead =
        "Force push may overwrite changes since your last fetch. Do you want to use the safer force with lease instead?";

    public const string ForceWithLeaseTooltip =
        "Force with lease is a safer way to force push. It ensures you only overwrite work that you have seen in your local repository";

    public static readonly string ConfigureRemote =
        $"Please configure a remote repository first.{Environment.NewLine}Would you like to do it now?";

    /// <summary>
    ///  Upstream's recursive submodules choices, stored as their index (<c>RecursiveSubmodules</c>).
    /// </summary>
    public static IReadOnlyList<string> RecursiveSubmoduleChoices { get; } = ["None", "Check", "On-demand"];

    private PushRepository _repository;
    private ConfigFileRemote? _remote;
    private bool _forceWithLease;
    private bool _force;
    private string _branch;

    /// <param name="branch">The branch to push; null for the current one.</param>
    public PushDialog(PushRepository repository, string? branch = null)
    {
        _repository = repository;
        branch ??= repository.CurrentBranch;
        _branch = branch.Length == 0 ? HeadText : branch;

        BindRemotes(null);
    }

    /// <summary>
    ///  Reads the repository again after the remotes window closed, choosing <paramref name="remote"/> when it still
    ///  exists (upstream's <c>OpenRemotesDialogAndRefreshList</c>).
    /// </summary>
    public void UpdateRepository(PushRepository repository, string? remote)
    {
        _repository = repository;
        BindRemotes(remote);
    }

    public IReadOnlyList<string> Remotes => [.. _repository.Remotes.Select(remote => remote.Name ?? "")];

    /// <summary>
    ///  The chosen remote, or empty when the repository has none.
    /// </summary>
    public string Remote
    {
        get => _remote?.Name ?? "";
        set
        {
            _remote = _repository.Remotes.FirstOrDefault(remote => remote.Name == value);
            ShowRemoteDestination();
            ProposeRemoteBranch();
        }
    }

    /// <summary>
    ///  The URL or folder the URL choice shows: the remote's push URL (else its URL) until the user types one.
    /// </summary>
    public string Destination { get; set; } = "";

    /// <summary>
    ///  True to push to <see cref="Destination"/> (upstream's "Url" choice).
    /// </summary>
    public bool ToUrl { get; set; }

    public PushTab Tab { get; set; }

    /// <summary>
    ///  "[ All ]", HEAD and the local branches, as upstream's branch list.
    /// </summary>
    public IReadOnlyList<string> Branches =>
        [AllRefs, HeadText, .. _repository.LocalBranches.Select(branch => branch.Name)];

    /// <summary>
    ///  The branch to push; choosing it proposes its branch on the remote, as upstream's BranchSelectedValueChanged.
    /// </summary>
    public string Branch
    {
        get => _branch;
        set
        {
            _branch = value;
            ProposeRemoteBranch();
        }
    }

    public string RemoteBranch { get; set; } = "";

    /// <summary>
    ///  Upstream's remote branch list: the branch itself, then the remote's other branches.
    /// </summary>
    public IReadOnlyList<string> RemoteBranches
    {
        get
        {
            List<string> branches = [];
            if (_branch.Length > 0 && _branch != HeadText && _branch != AllRefs)
            {
                branches.Add(_branch);
            }

            branches.AddRange(_repository.RemoteBranches.Where(branch => branch.Remote == Remote)
                .Select(branch => branch.LocalName).Where(name => name != _branch));
            return branches;
        }
    }

    public IReadOnlyList<string> Tags => [AllRefs, .. _repository.Tags];

    public string Tag { get; set; } = "";

    /// <summary>
    ///  Upstream's "Force with lease", which also forces a tag push (upstream binds the two boxes).
    /// </summary>
    public bool ForceWithLease
    {
        get => _forceWithLease;
        set
        {
            _forceWithLease = value;
            if (value)
            {
                _force = false;
            }
        }
    }

    public bool Force
    {
        get => _force;
        set
        {
            _force = value;
            if (value)
            {
                _forceWithLease = false;
            }
        }
    }

    public bool ReplaceTrackingReference { get; set; }

    public bool CreatePullRequest { get; set; }

    public int RecursiveSubmodules { get; set; }

    /// <summary>
    ///  The rows of the "Push multiple branches" tab for the chosen remote (upstream's LoadMultiBranchViewData, from the
    ///  remote branches the repository knows).
    /// </summary>
    public IReadOnlyList<PushBranchRow> MultipleBranches { get; private set; } = [];

    public string Title(string workingDirectory) => $"{PushCaption} ({workingDirectory})";

    /// <summary>
    ///  Fills the "Push multiple branches" rows for the chosen remote.
    /// </summary>
    public void LoadMultipleBranches()
    {
        string remote = Remote;
        List<IGitRef> remoteHeads = [.. _repository.RemoteBranches.Where(branch => branch.Remote == remote)];
        Dictionary<string, IGitRef> remoteByName = remoteHeads.ToDictionary(head => head.LocalName, head => head);
        List<PushBranchRow> rows = [];
        foreach (IGitRef head in _repository.LocalBranches)
        {
            string remoteName = head.Remote == remote ? head.MergeWith ?? head.LocalName : "";
            bool known = remoteByName.TryGetValue(head.Name, out IGitRef? remoteBranch);
            AheadBehindData aheadBehind = default;
            bool tracked = _repository.AheadBehind?.TryGetValue(head.Name, out aheadBehind) is true
                           && GitRefName.GetRemoteName(aheadBehind.RemoteRef) == remote;
            rows.Add(new PushBranchRow(head.Name,
                tracked ? GitRefName.GetRemoteBranch(aheadBehind.RemoteRef) : remoteName,
                tracked ? aheadBehind.ToDisplay()
                : !known ? ""
                : head.ObjectId == remoteBranch!.ObjectId ? "="
                : "<>"));
        }

        // The remote's branches with no local branch, to delete.
        rows.AddRange(remoteHeads
            .Where(remoteHead => _repository.LocalBranches.All(head => head.Name != remoteHead.LocalName))
            .Select(remoteHead => new PushBranchRow("", remoteHead.LocalName, "")));
        MultipleBranches = rows;
    }

    /// <summary>
    ///  Upstream's "Select all" and "Unselect all": the push choice of every row with a local branch.
    /// </summary>
    public void SelectAll(bool push)
    {
        foreach (PushBranchRow row in MultipleBranches.Where(row => row.Local.Length > 0))
        {
            row.Push = push;
        }
    }

    /// <summary>
    ///  Upstream's "Select tracked": push the rows whose local branch tracks a branch of the remote.
    /// </summary>
    public void SelectTracked()
    {
        foreach (PushBranchRow row in MultipleBranches)
        {
            row.Push = row.Local.Length > 0 && row.Remote.Length > 0;
        }
    }

    /// <summary>
    ///  The message upstream shows instead of pushing, or null.
    /// </summary>
    public string? ValidationError()
    {
        if (ToUrl && !Uri.IsWellFormedUriString(Destination, UriKind.Absolute))
        {
            return SelectDestinationDirectory;
        }

        if (_repository.Remotes.Count == 0)
        {
            return ConfigureRemote;
        }

        if (Tab == PushTab.Tags && string.IsNullOrEmpty(Tag))
        {
            return SelectTag;
        }

        return Tab == PushTab.Branches && _branch != AllRefs &&
               (string.IsNullOrWhiteSpace(_branch) || _branch == DetachedHeadParser.DetachedBranch ||
                string.IsNullOrWhiteSpace(RemoteBranch) || RemoteBranch == DetachedHeadParser.DetachedBranch)
            ? NoCurrentBranch
            : null;
    }

    /// <summary>
    ///  True when upstream asks before pushing a branch the remote does not seem to have: a branch push to a remote that is
    ///  not the branch's push target and that the repository knows no such branch of.
    /// </summary>
    public bool IsNewBranchForRemote
        => Tab == PushTab.Branches && !ToUrl && !_repository.IsBare && _branch != AllRefs && _remote is not null &&
           RemoteBranch != _repository.DefaultPushRemote(_remote, _branch) &&
           !_repository.RemoteBranches.Any(branch => branch.Remote == Remote && branch.LocalName == RemoteBranch) &&
           !_repository.LocalBranches.Any(head => head.Name == RemoteBranch && head.TrackingRemote == Remote);

    /// <summary>
    ///  Upstream's question before a branch push: whether to add a tracking reference, as the branch has none. Null when
    ///  upstream does not ask (the reference is replaced anyway, the branch tracks one, or git's
    ///  <c>branch.autosetupmerge</c> is false).
    /// </summary>
    public string? TrackingQuestion
    {
        get
        {
            if (Tab != PushTab.Branches || ReplaceTrackingReference || string.IsNullOrWhiteSpace(RemoteBranch) ||
                _repository.LocalBranches.FirstOrDefault(head => head.Name == _branch) is not { } local ||
                !string.IsNullOrEmpty(local.TrackingRemote) ||
                _repository.Remotes.Any(remote =>
                    _branch.StartsWith(remote.Name!, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(_repository.AutoSetupMerge?.Trim(), "false", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return string.Format(UpdateTrackingReference, local.Name, RemoteBranch);
        }
    }

    /// <summary>
    ///  The push to run; <paramref name="track"/> is the answer to <see cref="TrackingQuestion"/>, if asked.
    /// </summary>
    public PushRequest Request(bool track = false)
    {
        string destination = ToUrl ? Destination : Remote.Trim();
        ForcePushOptions force = _force || (Tab == PushTab.Tags && _forceWithLease) ? ForcePushOptions.Force
            : _forceWithLease ? ForcePushOptions.ForceWithLease
            : ForcePushOptions.DoNotForce;
        track |= ReplaceTrackingReference;
        return Tab switch
        {
            PushTab.Tags => new PushRequest(destination, "", "", force, false,
                Tag == AllRefs ? PushKind.AllTags : PushKind.Tag, Tag == AllRefs ? "" : Tag),
            PushTab.MultipleBranches => new PushRequest(destination, "", "", ForcePushOptions.DoNotForce, false,
                PushKind.Multiple, Actions:
                [
                    .. MultipleBranches
                        .Select(row => (Row: row, Remote: row.Remote.Length > 0 ? row.Remote : row.Local))
                        .Where(pair => pair.Remote.Length > 0 && (pair.Row.Push || pair.Row.Force || pair.Row.Delete))
                        .Select(pair => pair.Row.Push || pair.Row.Force
                            ? new GitPushAction(pair.Row.Local, pair.Remote, pair.Row.Force)
                            : GitPushAction.DeleteRemoteBranch(pair.Remote)),
                ]),
            _ when _branch == AllRefs => new PushRequest(destination, "", "", force, track, PushKind.AllBranches,
                RecursiveSubmodules: RecursiveSubmodules),
            _ => new PushRequest(destination, _branch, RemoteBranch, force, track,
                RecursiveSubmodules: RecursiveSubmodules),
        };
    }

    // Upstream's BindRemotesDropDown: the remote asked for, else the branch's, else origin, else the first.
    private void BindRemotes(string? remote)
    {
        string? wanted = string.IsNullOrEmpty(remote) ? _repository.CurrentBranchRemote : remote;
        _remote = _repository.Remotes.FirstOrDefault(candidate =>
                      StringComparer.OrdinalIgnoreCase.Equals(candidate.Name, wanted))
                  ?? _repository.Remotes.FirstOrDefault(candidate =>
                      StringComparer.OrdinalIgnoreCase.Equals(candidate.Name, "origin"))
                  ?? _repository.Remotes.FirstOrDefault();
        ShowRemoteDestination();
        ProposeRemoteBranch();
    }

    // Upstream's RemotesUpdated: the URL box shows the remote's push URL, else its URL.
    private void ShowRemoteDestination()
    {
        if (_remote is not null)
        {
            Destination = string.IsNullOrEmpty(_remote.PushUrl) ? _remote.Url ?? "" : _remote.PushUrl;
        }
    }

    // Upstream's BranchSelectedValueChanged: the remote's push target for the branch, else the branch it tracks there,
    // else the remote's prefix and the branch's name.
    private void ProposeRemoteBranch()
    {
        if (_branch == AllRefs)
        {
            RemoteBranch = "";
            return;
        }

        if (_branch == HeadText)
        {
            return;
        }

        if (!ToUrl && _remote is not null &&
            _repository.LocalBranches.FirstOrDefault(head => head.Name == _branch) is { } local)
        {
            if (_repository.DefaultPushRemote(_remote, local.Name) is { Length: > 0 } pushTarget)
            {
                RemoteBranch = pushTarget;
                return;
            }

            if (local.TrackingRemote.Equals(_remote.Name, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(local.MergeWith))
            {
                RemoteBranch = local.MergeWith;
                return;
            }
        }

        RemoteBranch = $"{_remote?.Prefix}{_branch}";
    }
}
