using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUIPluginInterfaces;
using ResourceManager.CommitDataRenders;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>CommitSummaryUserControl</c> data: the commit the branch is created at.
/// </summary>
/// <param name="ShortId">The group box header: the short hash.</param>
/// <param name="Date">The commit date, relative (upstream's <c>FormatDateAsRelativeLocal</c>).</param>
/// <param name="Tags">The tags, joined and shortened as upstream; empty for none.</param>
/// <param name="Branches">The local branches, joined and shortened as upstream; empty for none.</param>
public sealed record CommitSummary(string ShortId, string Author, string Date, string Subject, string Tags, string Branches);

/// <summary>
///  Upstream's <c>FormCreateBranch</c> without its window: the proposed name, the commit to create the branch at, the
///  orphan options and their rules, the checks before creating, and the git commands that create it.
/// </summary>
public sealed class CreateBranchDialog
{
    // Upstream's texts.
    public const string NoRevisionSelected = "Select 1 revision to create the branch on.";
    public const string BranchNameIsEmpty = "Enter branch name.";
    public const string BranchNameIsNotValid = "“{0}” is not valid branch name.";
    public const string CreatingOrphanBranch = "Creating orphan branch (repository has no commits)";
    public const string InvalidCommitHash = "The given commit hash is not valid for this repository and was therefore discarded.";
    public const string ErrorCaption = "Error";

    // CommitSummaryUserControl's texts.
    public const string NoRevision = "No revision";
    public const string NotAvailable = "n/a";
    public const string TagsCaption = "Tag(s):";
    public const string BranchesCaption = "Branch(es):";

    private const int MaxBranchTagLength = 75;

    /// <param name="hasCommits">False when HEAD has no commit yet: only an orphan branch can be made.</param>
    /// <param name="namePrefix">The proposed name, e.g. a left panel folder's path.</param>
    public CreateBranchDialog(bool hasCommits, string? namePrefix = null)
    {
        IsOrphanOnly = !hasCommits;
        BranchName = namePrefix ?? "";
        if (IsOrphanOnly)
        {
            // Upstream's ConfigureForOrphanBranch.
            CreateOrphan = true;
            CheckoutAfterCreate = true;
        }
    }

    /// <summary>
    ///  Upstream's <c>ConfigureForOrphanBranch</c>: the repository has no commit, so no commit can be picked and the branch
    ///  is an orphan that is checked out.
    /// </summary>
    public bool IsOrphanOnly { get; }

    public string BranchName { get; set; }

    /// <summary>
    ///  The commit the branch is created at; null when none is chosen.
    /// </summary>
    public ObjectId? Revision { get; set; }

    public bool CheckoutAfterCreate { get; set; } = true;

    public bool CreateOrphan { get; private set; }

    public bool ClearOrphan { get; set; }

    public bool CanChangeRevision => !CreateOrphan;

    public bool CanChangeCheckout => !CreateOrphan;

    public bool CanChangeOrphan => !IsOrphanOnly;

    public bool CanClearOrphan => CreateOrphan;

    /// <summary>
    ///  Upstream's <c>chkCreateOrphan_CheckedChanged</c>: an orphan is always checked out, and starts from no picked commit.
    /// </summary>
    public void SetCreateOrphan(bool orphan)
    {
        if (!CanChangeOrphan)
        {
            return;
        }

        CreateOrphan = orphan;
        if (orphan)
        {
            CheckoutAfterCreate = true;
        }
    }

    /// <summary>
    ///  Upstream's name proposal: the commit's first branch, else its first tag, unless a name was given.
    /// </summary>
    public void ProposeName(IEnumerable<IGitRef> refs)
    {
        if (!string.IsNullOrWhiteSpace(BranchName))
        {
            return;
        }

        List<IGitRef> all = [.. refs];
        BranchName = (all.FirstOrDefault(r => !r.IsTag) ?? all.FirstOrDefault(r => r.IsTag))?.LocalName ?? "";
    }

    /// <summary>
    ///  Upstream's <c>cmdOk_Click</c> checks: a commit unless an orphan, a name, and a name git accepts. Returns the error
    ///  to show (its English text passed through <paramref name="translate"/> before the name is filled in), or null.
    /// </summary>
    public string? Validate(Func<string, bool> isValidBranchName, Func<string, string>? translate = null)
    {
        if (!CreateOrphan && Revision is not { IsZero: false, IsArtificial: false })
        {
            return Translate(NoRevisionSelected);
        }

        string name = BranchName.Trim();
        if (name.Length == 0)
        {
            return Translate(BranchNameIsEmpty);
        }

        return isValidBranchName(name) ? null : string.Format(Translate(BranchNameIsNotValid), name);

        string Translate(string english) => translate?.Invoke(english) ?? english;
    }

    /// <summary>
    ///  The git commands upstream runs: <c>checkout --orphan</c> (then <c>rm --force -r .</c> to clear), or <c>checkout
    ///  -b</c> or <c>branch</c> at the commit.
    /// </summary>
    public IReadOnlyList<ArgumentString> Commands()
    {
        string name = BranchName.Trim();
        if (CreateOrphan)
        {
            ArgumentString orphan = GitCommands.Git.Commands.CreateOrphan(name);
            return ClearOrphan ? [orphan, GitCommands.Git.Commands.Remove()] : [orphan];
        }

        return [GitCommands.Git.Commands.Branch(name, Revision ?? default, CheckoutAfterCreate)];
    }

    /// <summary>
    ///  Upstream's <c>CommitSummaryUserControl.Revision</c> texts for <paramref name="revision"/>.
    /// </summary>
    public static CommitSummary Summarize(GitRevision revision)
    {
        string tags = string.Join(", ", revision.Refs.Where(r => r.IsTag).Select(r => r.LocalName)).ShortenTo(MaxBranchTagLength);
        string branches = string.Join(", ", revision.Refs.Where(r => r.IsHead).Select(r => r.LocalName))
            .ShortenTo(MaxBranchTagLength);
        return new CommitSummary(revision.ObjectId.ToShortString(), revision.Author ?? "",
            new DateFormatter().FormatDateAsRelativeLocal(revision.CommitDate), revision.Subject, tags, branches);
    }
}
