using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormMergeSubmodule</c> (rules in <see cref="MergeSubmoduleDialog"/>): a conflicted
///  submodule's commits, opened in a new instance, a branch containing the local and remote commits checked out in it, or
///  its current commit staged. Closes its window with true once the submodule was staged.
/// </summary>
public sealed partial class MergeSubmoduleViewModel : DialogViewModel
{
    private readonly IGitModule _module;
    private readonly Func<string, IReadOnlyList<ObjectId>, Task<bool>>? _checkoutBranch;
    private readonly Action<string> _openSubmodule;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ConflictData _item;

    /// <param name="module">The repository with the conflict.</param>
    /// <param name="fileName">The submodule's path in the repository.</param>
    /// <param name="checkoutBranch">Upstream's <c>StartCheckoutBranch</c> in the submodule's folder, limited to the branches
    /// that contain the given commits; without it "Checkout Branch" is disabled.</param>
    /// <param name="openSubmodule">Opens the submodule's folder in a new instance.</param>
    public MergeSubmoduleViewModel(IDialogHost host, IGitModule module, string fileName,
        Func<string, IReadOnlyList<ObjectId>, Task<bool>>? checkoutBranch, Action<string> openSubmodule)
        : base(host)
    {
        _module = module;
        FileName = fileName;
        _checkoutBranch = checkoutBranch;
        _openSubmodule = openSubmodule;
    }

    public string FileName { get; }

    [ObservableProperty]
    public partial string? BaseCommit { get; private set; }

    [ObservableProperty]
    public partial string? LocalCommit { get; private set; }

    [ObservableProperty]
    public partial string? RemoteCommit { get; private set; }

    [ObservableProperty]
    public partial string? CurrentCommit { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckoutBranchCommand))]
    public partial bool CanCheckoutBranch { get; private set; }

    /// <summary>
    ///  Completes when the commits are shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Upstream's <c>FormMergeSubmodule_Load</c>. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        _item = await Task.Run(() => _module.GetConflictAsync(FileName));
        (BaseCommit, LocalCommit, RemoteCommit) = MergeSubmoduleDialog.Commits(_item, Host.Translate);
        CanCheckoutBranch = _checkoutBranch is not null && MergeSubmoduleDialog.CanCheckoutBranch(_item);
        await ShowCurrentAsync();
        _loaded.TrySetResult();
    });

    // Upstream's btRefresh_Click: the commit checked out in the submodule.
    [RelayCommand]
    private Task RefreshAsync() => GuardAsync(ShowCurrentAsync);

    // Upstream's btOpenSubmodule_Click: a new instance on the submodule.
    [RelayCommand]
    private void OpenSubmodule() => _openSubmodule(_module.GetSubmoduleFullPath(FileName));

    // Upstream's btCheckoutBranch_Click: a branch containing the local and remote commits checked out in the submodule,
    // then the submodule staged.
    [RelayCommand(CanExecute = nameof(CanCheckoutBranch))]
    private Task CheckoutBranchAsync() => GuardAsync(async () =>
    {
        if (_checkoutBranch is null
            || !await _checkoutBranch(_module.GetSubmoduleFullPath(FileName), MergeSubmoduleDialog.BranchCommits(_item)))
        {
            return;
        }

        await StageAsync();
        Host.Close(true);
    });

    [RelayCommand]
    private Task StageCurrentAsync() => GuardAsync(async () =>
    {
        await StageAsync();
        Host.Close(true);
    });

    private async Task ShowCurrentAsync()
        => CurrentCommit = await Task.Run(() =>
            _module.GetSubmodule(FileName).GetCurrentCheckout() is { IsZero: false } id ? id.ToString() : "");

    // Upstream's StageSubmodule: git add, with git's output shown when it says something.
    private async Task StageAsync()
    {
        string output = await Task.Run(() =>
            _module.GitExecutable.GetOutput(new GitArgumentBuilder("add") { "--", FileName.QuoteNE() }));
        if (!string.IsNullOrWhiteSpace(output))
        {
            await Host.ShowMessageAsync(string.Format(Host.Translate(MergeSubmoduleDialog.StageFilename), FileName)
                                        + Environment.NewLine + output);
        }
    }
}
