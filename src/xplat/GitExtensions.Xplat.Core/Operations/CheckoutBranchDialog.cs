using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  What <see cref="CheckoutBranchDialog"/> reads from the repository.
/// </summary>
/// <param name="LocalBranches">The local branches (upstream's <c>GetRefs(RefsFilter.Heads)</c> names).</param>
/// <param name="RemoteBranches">The remote-tracking branches ("origin/main").</param>
/// <param name="Remotes">The remotes' names.</param>
/// <param name="LocalTrackingBranch">Upstream's <c>GetLocalTrackingBranchName(remote, remoteBranch)</c>.</param>
/// <param name="IsDirty">Whether the working directory has changes; null when upstream's
/// <c>CheckForUncommittedChangesInCheckoutBranch</c> is off and it was not checked.</param>
/// <param name="ContainingLocal">With commits given, the local branches that contain them all.</param>
/// <param name="ContainingRemote">With commits given, the remote branches that contain them all.</param>
public sealed record CheckoutBranchSource(
    IReadOnlyList<string> LocalBranches,
    IReadOnlyList<string> RemoteBranches,
    IReadOnlyList<string> Remotes,
    Func<string, string, string?> LocalTrackingBranch,
    bool? IsDirty,
    IReadOnlyList<string>? ContainingLocal = null,
    IReadOnlyList<string>? ContainingRemote = null);

/// <summary>
///  Upstream's <c>FormCheckoutBranch</c> without its window: local or remote branches (or the branches containing given
///  commits), for a remote branch whether a local branch is reset, created with a custom name or not made, what happens to
///  local changes, the checks before checking out, and the checkout command.
/// </summary>
public sealed class CheckoutBranchDialog
{
    // Upstream's texts.
    public const string CustomBranchNameIsEmpty = "Custom branch name is empty.\nEnter valid branch name or select predefined value.";
    public const string CustomBranchNameIsNotValid = "“{0}” is not valid branch name.\nEnter valid branch name or select predefined value.";
    public const string CreateBranchText = "Cr&eate local branch with same name:";
    public const string ResetBranchText = "R&eset local branch with the name:";
    public const string ApplyStashedItemsAgainCaption = "Auto stash";
    public const string ApplyStashedItemsAgain = "Apply stashed items to working directory again?";
    public const string ResetNonFastForwardBranch =
        "You are going to reset the “{0}” branch to a new location discarding ALL the commited changes since the {1} revision.\n\nAre you sure?";
    public const string ResetCaption = "Reset branch";
    public const string InvalidBranchName = "An existing branch must be selected.";

    private readonly CheckoutBranchSource _source;
    private string _branch = "";

    /// <param name="branch">The branch to show selected (upstream adds it to the list).</param>
    /// <param name="remote">True to start with the remote branches.</param>
    public CheckoutBranchDialog(CheckoutBranchSource source, LocalChangesAction defaultAction, bool createLocalBranchForRemote,
        string branch = "", bool remote = false)
    {
        _source = source;
        IsRemote = remote;
        Branches = BranchesOfKind();

        // Upstream: with commits given and no branch of that kind containing them, the other kind is shown.
        if (IsContainingCommits && Branches.Count == 0)
        {
            IsRemote = !remote;
            Branches = BranchesOfKind();
        }

        if (!string.IsNullOrEmpty(branch))
        {
            Branches = [.. Branches, branch];
            Branch = branch;
        }
        else if (IsContainingCommits && Branches.Count == 1)
        {
            Branch = Branches[0];
        }

        ChangesMode = defaultAction;
        NewBranchMode = createLocalBranchForRemote ? CheckoutNewBranchMode.Create : CheckoutNewBranchMode.Reset;
    }

    public bool IsRemote { get; private set; }

    public IReadOnlyList<string> Branches { get; private set; }

    public bool IsContainingCommits => _source.ContainingLocal is not null || _source.ContainingRemote is not null;

    /// <summary>
    ///  The chosen branch; setting it computes the local branch names upstream's <c>Branches_SelectedIndexChanged</c> does.
    /// </summary>
    public string Branch
    {
        get => _branch;
        set
        {
            _branch = value;
            if (string.IsNullOrWhiteSpace(value) || !IsRemote)
            {
                RemoteName = LocalBranchName = CustomBranchName = "";
                return;
            }

            RemoteName = GitRefName.GetRemoteName(value, _source.Remotes);
            LocalBranchName = _source.LocalTrackingBranch(RemoteName, value) ?? "";
            string remoteBranchName = RemoteName.Length > 0 ? value[(RemoteName.Length + 1)..] : value;
            CustomBranchName = $"{RemoteName}_{remoteBranchName}";

            // As upstream, the numbered proposals are made from the local branch's name.
            int i = 2;
            while (LocalBranchExists(CustomBranchName))
            {
                CustomBranchName = $"{RemoteName}_{LocalBranchName}_{i}";
                i++;
            }
        }
    }

    public string RemoteName { get; private set; } = "";

    /// <summary>
    ///  The local branch a remote branch's checkout resets or creates (upstream's <c>branchName</c> label).
    /// </summary>
    public string LocalBranchName { get; private set; } = "";

    public string CustomBranchName { get; set; } = "";

    /// <summary>
    ///  Upstream's <c>rbResetBranch</c> text: reset when the local branch exists, else create it with the same name.
    /// </summary>
    public string ResetOptionText => LocalBranchExists(LocalBranchName) ? ResetBranchText : CreateBranchText;

    public CheckoutNewBranchMode NewBranchMode { get; set; }

    public bool HasUncommittedChanges => _source.IsDirty ?? true;

    public LocalChangesAction ChangesMode { get; set; }

    /// <summary>
    ///  Upstream's "Set as default": not offered for Reset.
    /// </summary>
    public bool SetAsDefault { get; set; }

    public bool CanSetAsDefault => ChangesMode != LocalChangesAction.Reset;

    /// <summary>
    ///  Shows the local or the remote branches; the chosen branch is cleared, as upstream's <c>PopulateBranches</c>.
    /// </summary>
    public void SetRemote(bool remote)
    {
        if (remote == IsRemote)
        {
            return;
        }

        IsRemote = remote;
        Branches = BranchesOfKind();
        Branch = IsContainingCommits && Branches.Count == 1 ? Branches[0] : "";
    }

    /// <summary>
    ///  Upstream's <c>DoDefaultActionOrShow</c>: a local branch is checked out without the window unless it is always
    ///  shown, or there are local changes and the default action is not to be used.
    /// </summary>
    public bool ChecksOutWithoutWindow(bool alwaysShow, bool useDefaultAction)
        => !alwaysShow && !string.IsNullOrWhiteSpace(Branch) && !IsRemote && (!HasUncommittedChanges || useDefaultAction);

    /// <summary>
    ///  Upstream's checks before checking out: an existing branch, and a custom name that git accepts. Returns the error to
    ///  show (its English text passed through <paramref name="translate"/> before the name is filled in), or null.
    /// </summary>
    public string? Validate(Func<string, bool> isValidBranchName, Func<string, string>? translate = null)
    {
        if (!Branches.Contains(Branch.Trim()))
        {
            return Translate(InvalidBranchName);
        }

        if (IsRemote && NewBranchMode == CheckoutNewBranchMode.Create)
        {
            string name = CustomBranchName.Trim();
            if (name.Length == 0)
            {
                return Translate(CustomBranchNameIsEmpty);
            }

            if (!isValidBranchName(name))
            {
                return string.Format(Translate(CustomBranchNameIsNotValid), name);
            }
        }

        return null;

        string Translate(string english) => translate?.Invoke(english) ?? english;
    }

    /// <summary>
    ///  Upstream's <c>PerformCheckout</c> choice of what happens to local changes: none without changes, or when the
    ///  window was not shown and the default action is not to be used.
    /// </summary>
    public LocalChangesAction EffectiveChanges(bool shown, bool useDefaultAction)
        => (!shown && !useDefaultAction) || !HasUncommittedChanges ? LocalChangesAction.DontChange : ChangesMode;

    /// <summary>
    ///  The new default for local changes when "Set as default" is checked (never Reset), else null.
    /// </summary>
    public LocalChangesAction? NewDefaultAction => SetAsDefault && ChangesMode != LocalChangesAction.Reset ? ChangesMode : null;

    /// <summary>
    ///  Upstream's <c>Commands.CheckoutBranch</c> for the choices; a stash is made and popped around it, not by git.
    /// </summary>
    public ArgumentString Arguments(LocalChangesAction localChanges)
    {
        (CheckoutNewBranchMode mode, string? name) = !IsRemote ? (CheckoutNewBranchMode.DontCreate, null)
            : NewBranchMode switch
            {
                CheckoutNewBranchMode.Create => (CheckoutNewBranchMode.Create, CustomBranchName.Trim()),
                CheckoutNewBranchMode.Reset => (CheckoutNewBranchMode.Reset, LocalBranchName),
                _ => (CheckoutNewBranchMode.DontCreate, null),
            };
        return Commands.CheckoutBranch(Branch.Trim(), IsRemote, localChanges, mode, name).Arguments;
    }

    /// <summary>
    ///  Upstream's warning before a reset that is not a fast-forward: the local branch loses its commits since the merge
    ///  base. Null when the reset only moves the branch forward.
    /// </summary>
    public string? ResetWarning(ObjectId local, ObjectId remote, ObjectId mergeBase, Func<string, string>? translate = null)
    {
        if (!IsRemote || NewBranchMode != CheckoutNewBranchMode.Reset || local.IsZero || remote.IsZero || local == mergeBase)
        {
            return null;
        }

        string mergeBaseText = mergeBase.IsZero ? "merge base" : mergeBase.ToShortString();
        return string.Format(translate?.Invoke(ResetNonFastForwardBranch) ?? ResetNonFastForwardBranch, LocalBranchName, mergeBaseText);
    }

    /// <summary>
    ///  Upstream's <c>GetContainsObjectIdBranches</c>: the branches that contain every one of <paramref name="ids"/>,
    ///  without a detached HEAD or a remote's HEAD.
    /// </summary>
    /// <param name="branchesContaining">Upstream's <c>GetAllBranchesWhichContainGivenCommit</c> for one kind of branch.</param>
    public static IReadOnlyList<string> ContainingBranches(IReadOnlyList<ObjectId> ids,
        Func<ObjectId, IEnumerable<string>> branchesContaining)
    {
        HashSet<string> result = [];
        for (int index = 0; index < ids.Count; index++)
        {
            IEnumerable<string> branches = branchesContaining(ids[index])
                .Where(branch => !DetachedHeadParser.IsDetachedHead(branch) && !branch.EndsWith("/HEAD"));
            if (index == 0)
            {
                result.UnionWith(branches);
            }
            else
            {
                result.IntersectWith(branches);
            }
        }

        return [.. result];
    }

    private IReadOnlyList<string> BranchesOfKind()
        => [.. (IsRemote
                ? _source.ContainingRemote ?? _source.RemoteBranches
                : _source.ContainingLocal ?? _source.LocalBranches)
            .Where(name => !string.IsNullOrWhiteSpace(name))];

    private bool LocalBranchExists(string name)
        => _source.LocalBranches.Any(head => head.Equals(name, StringComparison.OrdinalIgnoreCase));
}
