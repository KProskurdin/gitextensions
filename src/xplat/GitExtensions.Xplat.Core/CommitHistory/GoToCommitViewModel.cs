using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  The control of upstream's <c>FormGoToCommit</c> that supplies the revision: the one last focused.
/// </summary>
public enum GoToCommitSource
{
    Expression,
    Tag,
    Branch,
}

/// <summary>
///  The view model of upstream's <c>FormGoToCommit</c> (rules in <see cref="GoToCommit"/>): a commit expression, a tag or
///  a branch to go to, whichever <see cref="Source"/> says (upstream's <c>SetSelectedRevisionByFocusedControl</c>). Closes
///  its window with the commit git resolves it to (zero when git knows none).
/// </summary>
public sealed partial class GoToCommitViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormGoToCommit";

    private readonly IGitModule _module;
    private readonly Func<Task<string?>> _readClipboard;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IReadOnlyList<IGitRef> _tags = [];
    private IReadOnlyList<IGitRef> _branches = [];
    private bool _going;

    /// <param name="module">The repository.</param>
    /// <param name="readClipboard">Reads the clipboard's text; null when it has none.</param>
    public GoToCommitViewModel(IDialogHost host, IGitModule module, Func<Task<string?>> readClipboard)
        : base(host)
    {
        _module = module;
        _readClipboard = readClipboard;
    }

    [ObservableProperty]
    public partial string? CommitExpression { get; set; }

    [ObservableProperty]
    public partial string? TagText { get; set; }

    [ObservableProperty]
    public partial string? BranchText { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> TagNames { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<string> BranchNames { get; private set; } = [];

    /// <summary>
    ///  The lists' placeholder while the refs are read; null once they are listed.
    /// </summary>
    [ObservableProperty]
    public partial string? LoadingText { get; private set; }

    /// <summary>
    ///  The control that supplies the revision; the window sets it when one is focused.
    /// </summary>
    public GoToCommitSource Source { get; set; }

    /// <summary>
    ///  True when <see cref="CommitExpression"/> came from the clipboard, which the window then selects.
    /// </summary>
    public bool ExpressionFromClipboard { get; private set; }

    /// <summary>
    ///  Completes when the tags and branches are listed and the clipboard was read, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Upstream's <c>FormGoToCommit_Load</c>: the clipboard's text when it names a commit, the tags and the branches. The
    ///  window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        LoadingText = Host.Translate(CreateWorktreeDialog.LoadingData);
        await SetCommitExpressionFromClipboardAsync();
        (_tags, _branches) = await Task.Run(() =>
            (GoToCommit.Refs(_module, RefsFilter.Tags), GoToCommit.Refs(_module, RefsFilter.Heads)));
        LoadingText = null;
        TagNames = [.. _tags.Select(tag => tag.LocalName)];
        BranchNames = [.. _branches.Select(branch => branch.LocalName)];
        _loaded.TrySetResult();
    });

    // Upstream's Go: the revision of the control that supplies it, as git resolves it.
    [RelayCommand]
    private Task GoAsync() => GuardAsync(async () =>
    {
        if (_going)
        {
            return;
        }

        _going = true;
        string revision = Source switch
        {
            GoToCommitSource.Tag => RefHash(_tags, TagText),
            GoToCommitSource.Branch => RefHash(_branches, BranchText),
            _ => CommitExpression?.Trim() ?? "",
        };
        Host.Close(await Task.Run(() => GoToCommit.Resolve(_module, revision)));
    });

    // Upstream's SetCommitExpressionFromClipboard.
    private async Task SetCommitExpressionFromClipboardAsync()
    {
        string text = (await _readClipboard())?.Trim() ?? "";
        if (text.Length > 0 && !(await Task.Run(() => GoToCommit.Resolve(_module, text))).IsZero)
        {
            CommitExpression = text;
            ExpressionFromClipboard = true;
        }
    }

    private static string RefHash(IReadOnlyList<IGitRef> refs, string? name)
        => refs.FirstOrDefault(gitRef => gitRef.LocalName == name)?.Guid ?? "";
}
