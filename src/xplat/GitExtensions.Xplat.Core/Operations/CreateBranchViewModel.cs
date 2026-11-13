using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormCreateBranch</c> (the create branch window): reads the commit the branch starts
///  at, keeps the dialog state (<see cref="CreateBranchDialog"/>) and the commit summary, and creates the branch. Closes
///  its window with true once the branch is created; git's failure leaves it open.
/// </summary>
public sealed partial class CreateBranchViewModel : DialogViewModel
{
    private readonly IGitModule _module;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private readonly string? _revision;
    private readonly string? _namePrefix;
    private readonly bool _checkoutAfterCreation;
    private readonly bool _userAbleToChangeRevision;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ObjectId _originalCheckout;
    private bool _isLoaded;
    private int _summaryVersion;

    /// <param name="revision">The commit to create the branch at (a hash or ref); HEAD when null or an artificial commit.</param>
    /// <param name="namePrefix">The proposed name, e.g. a left panel folder's path with a trailing slash.</param>
    /// <param name="checkoutAfterCreation">Upstream's <c>CheckoutAfterCreation</c>: "Checkout after create" to start with.</param>
    /// <param name="userAbleToChangeRevision">Upstream's <c>UserAbleToChangeRevision</c>.</param>
    /// <param name="couldBeOrphan">Upstream's <c>CouldBeOrphan</c>: the orphan options can be used.</param>
    public CreateBranchViewModel(IDialogHost host, IGitModule module, RepositoryOperationsViewModel actions,
        IAppPreferences preferences, string? revision = null, string? namePrefix = null,
        bool checkoutAfterCreation = true,
        bool userAbleToChangeRevision = true, bool couldBeOrphan = true)
        : base(host)
    {
        _module = module;
        _actions = actions;
        _preferences = preferences;
        _revision = revision;
        _namePrefix = namePrefix;
        _checkoutAfterCreation = checkoutAfterCreation;
        _userAbleToChangeRevision = userAbleToChangeRevision;
        CouldBeOrphan = couldBeOrphan;
        Dialog = new CreateBranchDialog(hasCommits: true, namePrefix);
    }

    /// <summary>
    ///  The dialog state; replaced once the commit is read, because a repository without commits only makes an orphan.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPickRevision))]
    [NotifyPropertyChangedFor(nameof(OrphanOnlyText))]
    public partial CreateBranchDialog Dialog { get; private set; }

    /// <summary>
    ///  Upstream's <c>CommitSummaryUserControl</c> of the chosen commit; null for none.
    /// </summary>
    [ObservableProperty]
    public partial CommitSummary? Summary { get; private set; }

    /// <summary>
    ///  The repository the commit picker resolves commits in.
    /// </summary>
    public IGitModule Module => _module;

    /// <summary>
    ///  Upstream's commit picker state: off for an orphan and when the caller fixed the commit.
    /// </summary>
    public bool CanPickRevision => Dialog.CanChangeRevision && _userAbleToChangeRevision;

    /// <summary>
    ///  Upstream's <c>CouldBeOrphan</c>: the orphan group is enabled.
    /// </summary>
    public bool CouldBeOrphan { get; }

    /// <summary>
    ///  Upstream's <c>ConfigureForOrphanBranch</c> label, which takes the commit picker's place; null while a commit can be
    ///  picked.
    /// </summary>
    public string? OrphanOnlyText =>
        Dialog.IsOrphanOnly ? Host.Translate(CreateBranchDialog.CreatingOrphanBranch) : null;

    /// <summary>
    ///  True when the created branch was checked out at another commit than before, which upstream follows with a
    ///  submodule update.
    /// </summary>
    public bool CheckedOutOtherCommit { get; private set; }

    /// <summary>
    ///  Completes when <see cref="LoadAsync"/> is done, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Reads the commit, proposes the name from its refs and applies the caller's choices over it (upstream's
    ///  <c>OnShown</c>). The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        try
        {
            (ObjectId current, ObjectId start, GitRevision? revision) = await Task.Run(() =>
            {
                ObjectId head = _module.GetCurrentCheckout();
                ObjectId id = _revision is null ? default : _module.RevParse(_revision);
                if (id.IsZero || id.IsArtificial)
                {
                    id = head;
                }

                return (head, id, id.IsZero ? null : _module.GetRevision(id, shortFormat: true, loadRefs: true));
            });
            _originalCheckout = current;
            CreateBranchDialog dialog = new(hasCommits: !start.IsZero, _namePrefix);
            if (revision is not null)
            {
                dialog.ProposeName(revision.Refs);
                dialog.Revision = start;
            }

            dialog.CheckoutAfterCreate = _checkoutAfterCreation;
            Dialog = dialog;
            Summary = revision is null ? null : CreateBranchDialog.Summarize(revision);
            _isLoaded = true;
        }
        finally
        {
            _loaded.TrySetResult();
        }
    });

    /// <summary>
    ///  Upstream's <c>BranchNameTextBox_Leave</c>: the name normalised when "Auto normalise branch name" is on. The window
    ///  calls it when the name box loses focus.
    /// </summary>
    public void NormaliseName()
        => Dialog.BranchName = BranchNames.Normalise(Dialog.BranchName, _preferences.AutoNormaliseBranchName,
            _preferences.AutoNormaliseSymbol);

    // The generated signature says oldValue is never null, but the first assignment passes null.
    partial void OnDialogChanged(CreateBranchDialog oldValue, CreateBranchDialog newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnDialogPropertyChanged;
        }

        newValue.PropertyChanged += OnDialogPropertyChanged;
    }

    // Upstream's cmdOk_Click: the checks, then the commands; the window stays open when git fails.
    [RelayCommand]
    private Task CreateAsync() => GuardAsync(async () =>
    {
        NormaliseName();
        CreateBranchDialog dialog = Dialog;
        string? error = await Task.Run(() => dialog.Validate(_module.CheckBranchFormat, Host.Translate));
        if (error is not null)
        {
            await Host.ShowMessageAsync(error);
            return;
        }

        if (await _actions.CreateBranchAsync(_module.WorkingDir, dialog))
        {
            CheckedOutOtherCommit = dialog is { CheckoutAfterCreate: true, CreateOrphan: false, Revision: { } start }
                                    && start != _originalCheckout;
            Host.Close(true);
        }
    });

    [RelayCommand]
    private void Cancel() => Host.Close(false);

    private void OnDialogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CreateBranchDialog.CreateOrphan):
                OnPropertyChanged(nameof(CanPickRevision));
                break;
            case nameof(CreateBranchDialog.Revision) when _isLoaded && Dialog.Revision is { } id:
                Run(() => ShowSummaryAsync(id));
                break;
        }
    }

    // Upstream's commitPicker_SelectedObjectIdChanged: the summary of the picked commit; a later pick wins.
    private async Task ShowSummaryAsync(ObjectId id)
    {
        int version = ++_summaryVersion;
        GitRevision revision = await Task.Run(() => _module.GetRevision(id, shortFormat: true, loadRefs: true));
        if (version == _summaryVersion)
        {
            Summary = CreateBranchDialog.Summarize(revision);
        }
    }
}
