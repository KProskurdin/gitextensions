using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormCherryPick</c> (rules in <see cref="CherryPickDialog"/>): the commit to pick (another
///  one chosen in upstream's FormChooseCommit), the parent of a merge, whether to commit and to add the reference. Cherry
///  pick runs the caller's pick (upstream's FormProcess and conflict handling) and closes the window with true; the options
///  are kept then.
/// </summary>
public sealed partial class CherryPickViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormCherryPick";

    private readonly IGitModule _module;
    private readonly IAppPreferences _preferences;
    private readonly Func<string, ArgumentString, Task>? _pick;
    private readonly Func<string?, Task<string?>> _chooseCommit;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? _revision;
    private GitRevision? _selected;

    /// <param name="revision">The commit to cherry pick.</param>
    /// <param name="preferences">Upstream's two cherry pick options.</param>
    /// <param name="pick">Runs the cherry pick command (the commit's hash, the command) and handles its conflicts.</param>
    /// <param name="chooseCommit">Upstream's FormChooseCommit on the given commit: the chosen commit's hash, or null.</param>
    public CherryPickViewModel(IDialogHost host, IGitModule module, string revision, IAppPreferences preferences,
        Func<string, ArgumentString, Task>? pick, Func<string?, Task<string?>> chooseCommit)
        : base(host)
    {
        _module = module;
        _revision = revision;
        _preferences = preferences;
        _pick = pick;
        _chooseCommit = chooseCommit;
        Parents = [];

        // Upstream's LoadSettings.
        AutoCommit = preferences.CommitAutomaticallyAfterCherryPick;
        AddReference = preferences.AddCommitReferenceToCherryPick;
    }

    /// <summary>
    ///  Upstream's <c>CommitSummaryUserControl</c> of the commit; null when git does not know it.
    /// </summary>
    [ObservableProperty]
    public partial CommitSummary? Summary { get; private set; }

    /// <summary>
    ///  The commit is a merge, so a parent is chosen.
    /// </summary>
    [ObservableProperty]
    public partial bool IsMerge { get; private set; }

    /// <summary>
    ///  A merge's parents, numbered from one.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<CherryPickParent> Parents { get; private set; }

    [ObservableProperty]
    public partial CherryPickParent? SelectedParent { get; set; }

    /// <summary>
    ///  Upstream's "Automatically create a commit".
    /// </summary>
    [ObservableProperty]
    public partial bool AutoCommit { get; set; }

    /// <summary>
    ///  Upstream's "Add commit reference to commit message".
    /// </summary>
    [ObservableProperty]
    public partial bool AddReference { get; set; }

    /// <summary>
    ///  Completes when the commit and its parents are shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Reads the commit to pick. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        try
        {
            await ShowRevisionAsync();
        }
        finally
        {
            _loaded.TrySetResult();
        }
    });

    // Upstream's btnChooseRevision_Click.
    [RelayCommand]
    private Task ChooseRevisionAsync() => GuardAsync(async () =>
    {
        if (await _chooseCommit(_selected?.Guid) is { } hash)
        {
            _revision = hash;
        }

        await ShowRevisionAsync();
    });

    // Upstream's btnPick_Click and SaveSettings.
    [RelayCommand]
    private Task PickAsync() => GuardAsync(async () =>
    {
        int? parent = null;
        if (IsMerge)
        {
            if (SelectedParent is not { } chosen)
            {
                await Host.ShowMessageAsync(Host.Translate(CherryPickDialog.NoneParentSelected),
                    Host.Translate(ManageWorktreeDialog.Error));
                return;
            }

            parent = chosen.Number;
        }

        if (_selected is null)
        {
            return;
        }

        bool autoCommit = AutoCommit;
        bool addReference = AddReference;
        if (_pick is not null)
        {
            await _pick(_selected.Guid, CherryPickDialog.Arguments(_selected.ObjectId, autoCommit, parent, addReference));
        }

        _preferences.CommitAutomaticallyAfterCherryPick = autoCommit;
        _preferences.AddCommitReferenceToCherryPick = addReference;
        Host.Close(true);
    });

    [RelayCommand]
    private void Cancel() => Host.Close(false);

    // Upstream's OnRevisionChanged: the summary, and for a merge its parents with the first chosen.
    private async Task ShowRevisionAsync()
    {
        (GitRevision? selected, bool isMerge, IReadOnlyList<CherryPickParent> parents) =
            await Task.Run(() => CherryPickDialog.ReadCommit(_module, _revision));
        _selected = selected;
        Summary = selected is null ? null : CreateBranchDialog.Summarize(selected);
        IsMerge = isMerge;
        Parents = parents;
        SelectedParent = isMerge ? Parents.FirstOrDefault() : null;
    }
}
