using GitCommands;
using GitExtensions.Extensibility;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  A branch or tag in one of the grid menu's submenus: the name shown, whether it is a remote branch, and the name the
///  command gets (upstream's unambiguous name for a merge, or the commit's hash when nothing else can be merged).
/// </summary>
public sealed record GridMenuRef(string Name, bool IsRemote, string? Argument = null)
{
    /// <summary>
    ///  The name the command gets.
    /// </summary>
    public string Target => Argument ?? Name;

    /// <summary>
    ///  Upstream's <c>AddBranchMenuItem</c> picture: a remote or a local branch (tags get the local one, as upstream's).
    /// </summary>
    public string Image => IsRemote ? "BranchRemote" : "BranchLocal";
}

/// <summary>
///  The grid menu's submenus for a commit, as upstream's <c>ContextMenuOpening</c> fills them: null entries are separators.
/// </summary>
public sealed record GridRefMenus(
    IReadOnlyList<GridMenuRef?> Checkout,
    IReadOnlyList<GridMenuRef> Push,
    IReadOnlyList<GridMenuRef> Merge,
    string? RebaseOnTopOf,
    IReadOnlyList<GridMenuRef> Rename,
    IReadOnlyList<GridMenuRef?> Delete,
    IReadOnlyList<GridMenuRef> DeleteTags,
    IReadOnlyList<GridMenuRef> SelectInLeftPanel,
    bool IsHeadOfCurrentBranch);

/// <summary>
///  An entry of upstream's "Copy to clipboard" submenu (<c>CopyContextMenuItem</c>): its text with the access key, the text
///  copied and its picture; a caption copies nothing. Null entries are separators.
/// </summary>
public sealed record GridCopyItem(string Text, string? Value, string? Image);

/// <summary>
///  Upstream's revision grid context menu rules (<c>RevisionGridControl.ContextMenuOpening</c>, <c>GitRefListsForRevision</c>,
///  <c>CopyContextMenuItem</c>) for one selected commit.
/// </summary>
public static class GridMenus
{
    // CopyContextMenuItem: a value is shown after its caption up to this length.
    private const int CopyValueLength = 40;

    // CopyContextMenuItem: refs get the access keys 1 to 9 and 0, in order.
    private const int NumberedRefs = 10;

    /// <summary>
    ///  The submenus for <paramref name="row"/>; <paramref name="currentBranch"/> is the checked-out branch ("" when
    ///  detached) and <paramref name="ambiguousNames"/> the names that are both a branch and a tag, which upstream merges
    ///  by their full name.
    /// </summary>
    public static GridRefMenus For(CommitRow row, string currentBranch, IReadOnlySet<string> ambiguousNames)
    {
        bool isArtificial = ArtificialCommits.IsArtificial(row.Hash);
        IReadOnlyList<RefLabel> labels = row.Labels ?? [];
        RefLabel[] allBranches = [.. labels.Where(label => label.Kind is RefKind.Branch or RefKind.RemoteBranch)];
        RefLabel[] localBranches = [.. allBranches.Where(label => label.Kind == RefKind.Branch)];
        RefLabel[] tags = [.. labels.Where(label => label.Kind == RefKind.Tag)];

        // GitRefListsForRevision: a remote branch that a local branch here tracks is left out of the merge list.
        RefLabel[] withNoIdenticalRemotes =
        [
            .. allBranches.Where(branch => branch.Kind == RefKind.Branch
                                           || !localBranches.Any(local => local.Upstream == branch.Name))
        ];

        List<GridMenuRef> deleteTags = [];
        List<GridMenuRef> selectInLeftPanel = [];
        List<GridMenuRef> merge = [];
        foreach (RefLabel tag in tags)
        {
            deleteTags.Add(new GridMenuRef(tag.Name, IsRemote: false));
            selectInLeftPanel.Add(new GridMenuRef(tag.Name, IsRemote: false));
            merge.Add(new GridMenuRef(tag.Name, IsRemote: false, Unambiguous(tag)));
        }

        bool currentBranchPointsToRevision = false;
        foreach (RefLabel branch in withNoIdenticalRemotes)
        {
            if (IsCurrent(branch))
            {
                currentBranchPointsToRevision = !isArtificial;
            }
            else
            {
                merge.Add(new GridMenuRef(branch.Name, branch.Kind == RefKind.RemoteBranch, Unambiguous(branch)));
            }
        }

        // Upstream keeps the first branch's menu item tag, which is never set for a branch, so the rebase is onto the
        // commit itself unless the current branch points at it.
        string? rebaseOnTopOf = currentBranchPointsToRevision ? null : row.Hash;
        if (merge.Count == 0 && !currentBranchPointsToRevision)
        {
            merge.Add(new GridMenuRef(row.Hash, IsRemote: false));
        }

        List<GridMenuRef?> checkout = [];
        List<GridMenuRef> push = [];
        List<GridMenuRef> rename = [];
        List<GridMenuRef?> delete = [];
        bool isHeadOfCurrentBranch = false;
        bool firstRemoteBranchForCheckout = false;
        foreach (RefLabel branch in allBranches)
        {
            bool isRemote = branch.Kind == RefKind.RemoteBranch;
            selectInLeftPanel.Add(new GridMenuRef(branch.Name, isRemote));
            if (!isRemote)
            {
                if (IsCurrent(branch))
                {
                    isHeadOfCurrentBranch = true;
                }
                else
                {
                    delete.Add(new GridMenuRef(branch.Name, IsRemote: false));
                }

                rename.Add(new GridMenuRef(branch.Name, IsRemote: false));
                push.Add(new GridMenuRef(branch.Name, IsRemote: false));
            }

            if (IsCurrent(branch))
            {
                continue;
            }

            if (!isRemote)
            {
                firstRemoteBranchForCheckout = true;
            }
            else if (firstRemoteBranchForCheckout)
            {
                checkout.Add(null);
                firstRemoteBranchForCheckout = false;
            }

            checkout.Add(new GridMenuRef(branch.Name, isRemote));
        }

        bool firstRemoteBranchForDelete = true;
        foreach (RefLabel branch in allBranches.Where(branch => branch.Kind == RefKind.RemoteBranch))
        {
            if (firstRemoteBranchForDelete)
            {
                firstRemoteBranchForDelete = false;
                if (delete.Count > 0)
                {
                    delete.Add(null);
                }
            }

            delete.Add(new GridMenuRef(branch.Name, IsRemote: true));
        }

        return new GridRefMenus(checkout, push, merge, rebaseOnTopOf, rename, delete, deleteTags, selectInLeftPanel,
            isHeadOfCurrentBranch);

        bool IsCurrent(RefLabel branch) => branch.Kind == RefKind.Branch && branch.Name == currentBranch;

        // Upstream's GetRefUnambiguousName.
        string Unambiguous(RefLabel label)
            => ambiguousNames.Contains(label.Name) ? Prefix(label.Kind) + label.Name : label.Name;

        static string Prefix(RefKind kind)
            => kind switch
            {
                RefKind.Tag => GitRefName.RefsTagsPrefix,
                RefKind.RemoteBranch => GitRefName.RefsRemotesPrefix,
                _ => GitRefName.RefsHeadsPrefix,
            };
    }

    /// <summary>
    ///  Upstream's <c>CopyContextMenuItem</c> for one commit: the branches and the tags under captions (with the access
    ///  keys 1 to 0), then the hash, the message, the author and the date (or the author and commit dates when they
    ///  differ), each with the start of its value.
    /// </summary>
    /// <param name="branchesCaption">Upstream's "Branches" caption.</param>
    /// <param name="tagsCaption">Upstream's "Tags" caption.</param>
    public static IReadOnlyList<GridCopyItem?> CopyItems(CommitRow row, string branchesCaption, string tagsCaption)
    {
        List<GridCopyItem?> items = [];
        int number = 0;
        IReadOnlyList<RefLabel> labels = row.Labels ?? [];
        AddRefs(branchesCaption, labels.Where(label => label.Kind is RefKind.Branch or RefKind.RemoteBranch), "Branch");
        AddRefs(tagsCaption, labels.Where(label => label.Kind == RefKind.Tag), "Tag");

        AddValue(ResourceManager.TranslatedStrings.GetCommitHash(1), row.Hash, "CommitId", 'C');
        AddValue(ResourceManager.TranslatedStrings.GetMessage(1), row.Body ?? row.Subject, "Message", 'M');
        AddValue(ResourceManager.TranslatedStrings.GetAuthor(1), $"{row.Author} <{row.AuthorEmail}>", "Author", 'A');
        if (row.AuthorDateValue == row.CommitDateValue)
        {
            AddValue(ResourceManager.TranslatedStrings.Date, row.AuthorDateValue?.ToString() ?? "", "Date", 'D');
        }
        else
        {
            AddValue(ResourceManager.TranslatedStrings.GetAuthorDate(1), row.AuthorDateValue?.ToString() ?? "", "Date",
                'T');
            AddValue(ResourceManager.TranslatedStrings.GetCommitDate(1), row.CommitDateValue?.ToString() ?? "", "Date",
                'D');
        }

        return items;

        void AddRefs(string caption, IEnumerable<RefLabel> refs, string image)
        {
            List<string> names = [.. refs.Select(label => label.Name)];
            if (names.Count == 0)
            {
                return;
            }

            items.Add(new GridCopyItem(caption, Value: null, Image: null));
            foreach (string name in names)
            {
                // Upstream's PrependItemNumber.
                string text = ++number > NumberedRefs ? Escaped(name) : $"_{number % NumberedRefs}:   {Escaped(name)}";
                items.Add(new GridCopyItem(text, name, image));
            }

            items.Add(null);
        }

        void AddValue(string caption, string value, string image, char hotkey)
        {
            int position = caption.IndexOf(hotkey.ToString(), StringComparison.InvariantCultureIgnoreCase);
            string text = position >= 0 ? caption.Insert(position, "_") : caption;
            text += ":   " + Escaped(value.SubstringUntil('\n').ShortenTo(CopyValueLength));
            items.Add(new GridCopyItem(text.TrimEnd('\n', '\r'), value, image));
        }

        // An underscore in a name or value is shown, not taken as an access key.
        static string Escaped(string text) => text.Replace("_", "__", StringComparison.Ordinal);
    }
}
