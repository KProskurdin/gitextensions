using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  The view model of upstream's <c>FormRevisionFilter</c>: each filter turned on by its check box (since, until, author,
///  committer, message, diff content, ignore case, limit, path, branches), and the branch and history options, with
///  upstream's <c>UpdateFilters</c> rules. Closes its window with the new <see cref="RevisionFilter"/>, or null when
///  cancelled.
/// </summary>
public sealed partial class FilterViewModel : DialogViewModel
{
    private readonly RevisionFilter _current;
    private readonly int _defaultLimit;

    /// <param name="current">The filter in use.</param>
    /// <param name="defaultLimit">Upstream's <c>CommitsLimitDefault</c> (<c>MaxRevisionGraphCommits</c>), the limit shown
    /// while none is set.</param>
    public FilterViewModel(IDialogHost host, RevisionFilter current, int defaultLimit = 0)
        : base(host)
    {
        _current = current;
        _defaultLimit = defaultLimit;

        // Upstream's OnLoad: a date not set shows today.
        HasSince = current.Since is not null;
        Since = current.Since?.Date ?? DateTime.Today;
        HasUntil = current.Until is not null;
        Until = current.Until?.Date ?? DateTime.Today;
        HasAuthor = current.Author.Length > 0;
        Author = current.Author;
        HasCommitter = current.Committer.Length > 0;
        Committer = current.Committer;
        HasMessage = current.Message.Length > 0;
        Message = current.Message;
        HasDiffContent = current.DiffContent.Length > 0;
        DiffContent = current.DiffContent;
        IgnoreCase = current.IgnoreCase;
        HasCommitsLimit = current.CommitsLimit > 0;
        CommitsLimit = current.CommitsLimit > 0 ? current.CommitsLimit : defaultLimit;
        HasPathFilter = current.PathFilter.Length > 0;
        PathFilter = current.PathFilter;
        ShowFilteredBranches = current.ShowFilteredBranches;
        BranchFilter = current.BranchFilter;
        CurrentBranchOnly = current.CurrentBranchOnly;
        ShowReflog = current.ShowReflog;
        FirstParent = current.FirstParent;
        NoMerges = current.NoMerges;
        SimplifyByDecoration = current.SimplifyByDecoration;
        FullHistory = current.FullHistory;
        SimplifyMerges = current.SimplifyMerges;
    }

    [ObservableProperty]
    public partial bool HasSince { get; set; }

    [ObservableProperty]
    public partial DateTime? Since { get; set; }

    [ObservableProperty]
    public partial bool HasUntil { get; set; }

    [ObservableProperty]
    public partial DateTime? Until { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanIgnoreCase))]
    public partial bool HasAuthor { get; set; }

    [ObservableProperty]
    public partial string? Author { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanIgnoreCase))]
    public partial bool HasCommitter { get; set; }

    [ObservableProperty]
    public partial string? Committer { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanIgnoreCase))]
    public partial bool HasMessage { get; set; }

    [ObservableProperty]
    public partial string? Message { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanIgnoreCase))]
    public partial bool HasDiffContent { get; set; }

    [ObservableProperty]
    public partial string? DiffContent { get; set; }

    [ObservableProperty]
    public partial bool IgnoreCase { get; set; }

    [ObservableProperty]
    public partial bool HasCommitsLimit { get; set; }

    [ObservableProperty]
    public partial decimal? CommitsLimit { get; set; }

    [ObservableProperty]
    public partial bool HasPathFilter { get; set; }

    [ObservableProperty]
    public partial string? PathFilter { get; set; }

    [ObservableProperty]
    public partial bool ShowFilteredBranches { get; set; }

    [ObservableProperty]
    public partial string? BranchFilter { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanFilterBranches))]
    public partial bool CurrentBranchOnly { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowCurrentBranchOnly))]
    [NotifyPropertyChangedFor(nameof(CanFilterBranches))]
    public partial bool ShowReflog { get; set; }

    [ObservableProperty]
    public partial bool FirstParent { get; set; }

    [ObservableProperty]
    public partial bool NoMerges { get; set; }

    [ObservableProperty]
    public partial bool SimplifyByDecoration { get; set; }

    [ObservableProperty]
    public partial bool FullHistory { get; set; }

    [ObservableProperty]
    public partial bool SimplifyMerges { get; set; }

    /// <summary>
    ///  Upstream's <c>UpdateFilters</c>: ignore case matters while a text filter is on.
    /// </summary>
    public bool CanIgnoreCase => HasAuthor || HasCommitter || HasMessage || HasDiffContent;

    /// <summary>
    ///  Upstream's <c>UpdateFilters</c>: not with the reflog.
    /// </summary>
    public bool CanShowCurrentBranchOnly => !ShowReflog;

    /// <summary>
    ///  Upstream's <c>UpdateFilters</c>: not with the current branch only or the reflog.
    /// </summary>
    public bool CanFilterBranches => !CurrentBranchOnly && !ShowReflog;

    // Upstream's option_CheckedChanged: an unchecked limit shows the default again.
    partial void OnHasCommitsLimitChanged(bool value)
    {
        if (!value)
        {
            CommitsLimit = _defaultLimit;
        }
    }

    // Upstream's OkClick. "Until" means the whole day, as a user reading the date expects.
    [RelayCommand]
    private void Apply() => Host.Close(_current with
    {
        Since = HasSince ? Since?.Date : null,
        Until = HasUntil ? Until?.Date.AddDays(1).AddSeconds(-1) : null,
        Author = Value(HasAuthor, Author),
        Committer = Value(HasCommitter, Committer),
        Message = Value(HasMessage, Message),
        DiffContent = Value(HasDiffContent, DiffContent),
        IgnoreCase = IgnoreCase,
        CommitsLimit = HasCommitsLimit ? (int)(CommitsLimit ?? 0) : 0,
        PathFilter = Value(HasPathFilter, PathFilter),
        ShowFilteredBranches = ShowFilteredBranches,
        BranchFilter = BranchFilter?.Trim() ?? "",
        CurrentBranchOnly = CurrentBranchOnly,
        ShowReflog = ShowReflog,
        FirstParent = FirstParent,
        NoMerges = NoMerges,
        SimplifyByDecoration = SimplifyByDecoration,
        FullHistory = FullHistory,
        SimplifyMerges = SimplifyMerges,
    });

    [RelayCommand]
    private void Cancel() => Host.Close(null);

    private static string Value(bool enabled, string? text) => enabled ? text?.Trim() ?? "" : "";
}
