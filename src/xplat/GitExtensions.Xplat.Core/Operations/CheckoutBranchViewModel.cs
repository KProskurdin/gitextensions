using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormCheckoutBranch</c> (rules in <see cref="CheckoutBranchDialog"/>, the checkout in
///  the caller's flow): local or remote branches, for a remote branch reset or create a local branch or check out the
///  commit, and for local changes don't change, merge, stash or reset. Closes its window with true after checking out;
///  it stays open when the checkout did not happen.
/// </summary>
public sealed partial class CheckoutBranchViewModel : DialogViewModel
{
    private readonly IGitModule _module;
    private readonly Func<Task<bool>> _checkout;
    private int _countVersion;

    /// <param name="dialog">The dialog state the caller's flow checks out.</param>
    /// <param name="module">The repository, for the count of commits and the branch name check.</param>
    /// <param name="checkout">Checks out what the dialog describes; true when it did.</param>
    public CheckoutBranchViewModel(IDialogHost host, CheckoutBranchDialog dialog, IGitModule module,
        Func<Task<bool>> checkout)
        : base(host)
    {
        Dialog = dialog;
        _module = module;
        _checkout = checkout;
        dialog.PropertyChanged += OnDialogChanged;
        ShowBranch();
    }

    public CheckoutBranchDialog Dialog { get; }

    /// <summary>
    ///  Normalises the custom branch name when "Auto normalise branch name" is on, as upstream's
    ///  <c>txtCustomBranchName_Leave</c>; set by the flow.
    /// </summary>
    public Func<string, string> NormaliseName { get; set; } = name => name;

    /// <summary>
    ///  Upstream's "Remote branch" choice; changing it lists the other branches.
    /// </summary>
    public bool IsRemote
    {
        get => Dialog.IsRemote;
        set => Dialog.SetRemote(value);
    }

    /// <summary>
    ///  Upstream's <c>rbResetBranch</c> text, translated, with its access key.
    /// </summary>
    public string ResetText => Host.Translate(Dialog.ResetOptionText).Replace('&', '_');

    /// <summary>
    ///  Upstream's <c>branchName</c> label: the local branch in quotes.
    /// </summary>
    public string LocalBranchNameText => $"'{Dialog.LocalBranchName}'";

    /// <summary>
    ///  The custom name box is used for "Create local branch with custom name" only.
    /// </summary>
    public bool UsesCustomName => Dialog.NewBranchMode == CheckoutNewBranchMode.Create;

    /// <summary>
    ///  Upstream's count of commits between HEAD and the branch.
    /// </summary>
    [ObservableProperty]
    public partial string Changes { get; private set; } = "";

    /// <summary>
    ///  Upstream's <c>txtCustomBranchName_Leave</c>.
    /// </summary>
    public void NormaliseCustomName() => Dialog.CustomBranchName = NormaliseName(Dialog.CustomBranchName);

    // Upstream's Branches_SelectedIndexChanged, rbReset_CheckedChanged and the new branch choice.
    private void OnDialogChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CheckoutBranchDialog.IsRemote):
                OnPropertyChanged(nameof(IsRemote));
                break;
            case nameof(CheckoutBranchDialog.Branch):
                ShowBranch();
                break;
            case nameof(CheckoutBranchDialog.ResetOptionText):
                OnPropertyChanged(nameof(ResetText));
                break;
            case nameof(CheckoutBranchDialog.LocalBranchName):
                OnPropertyChanged(nameof(LocalBranchNameText));
                break;
            case nameof(CheckoutBranchDialog.NewBranchMode):
                OnPropertyChanged(nameof(UsesCustomName));
                break;
            case nameof(CheckoutBranchDialog.ChangesMode) when !Dialog.CanSetAsDefault:
                Dialog.SetAsDefault = false;
                break;
        }
    }

    // The count of commits against HEAD, read in the background for the latest branch.
    private void ShowBranch()
    {
        Changes = "";
        string branch = Dialog.Branch.Trim();
        int version = ++_countVersion;
        if (branch.Length == 0)
        {
            return;
        }

        Run(async () =>
        {
            string text = await Task.Run(() =>
            {
                ObjectId current = _module.GetCurrentCheckout();
                return current.IsZero ? "" : _module.GetCommitCountString(current, branch);
            });
            if (version == _countVersion)
            {
                Changes = text;
            }
        });
    }

    [RelayCommand]
    private Task CheckoutAsync() => GuardAsync(async () =>
    {
        NormaliseCustomName();
        string? error = await Task.Run(() => Dialog.Validate(_module.CheckBranchFormat, Host.Translate));
        if (error is not null)
        {
            await Host.ShowMessageAsync(error);
            return;
        }

        if (await _checkout())
        {
            Host.Close(true);
        }
    });

    [RelayCommand]
    private void Cancel() => Host.Close(false);
}
