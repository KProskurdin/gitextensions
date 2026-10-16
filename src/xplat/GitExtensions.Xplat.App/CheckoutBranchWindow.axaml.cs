using Avalonia.Controls;
using Avalonia.Input;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormCheckoutBranch</c> in the new shell (rules in <see cref="CheckoutBranchDialog"/>, the checkout in
///  <see cref="CheckoutBranchFlow"/>): local or remote branches, for a remote branch reset or create a local branch or
///  check out the commit, and for local changes don't change, merge, stash or reset. Closes with true after checking out;
///  stays open when the checkout did not happen.
/// </summary>
public partial class CheckoutBranchWindow : Window
{
    private readonly CheckoutBranchDialog _dialog;
    private readonly GitModule _module;
    private readonly Func<Window, Task<bool>> _checkout;
    private bool _showing;
    private int _countVersion;

    /// <param name="checkout">Checks out what the dialog describes; true when it did.</param>
    public CheckoutBranchWindow(CheckoutBranchDialog dialog, GitModule module, Func<Window, Task<bool>> checkout)
    {
        _dialog = dialog;
        _module = module;
        _checkout = checkout;
        InitializeComponent();

        LocalChangesGroup.IsVisible = dialog.HasUncommittedChanges;
        _showing = true;
        LocalBranchRadio.IsChecked = !dialog.IsRemote;
        RemoteBranchRadio.IsChecked = dialog.IsRemote;
        ResetBranchRadio.IsChecked = dialog.NewBranchMode == CheckoutNewBranchMode.Reset;
        CreateBranchRadio.IsChecked = dialog.NewBranchMode == CheckoutNewBranchMode.Create;
        DontCreateRadio.IsChecked = dialog.NewBranchMode == CheckoutNewBranchMode.DontCreate;
        DontChangeRadio.IsChecked = dialog.ChangesMode == LocalChangesAction.DontChange;
        MergeRadio.IsChecked = dialog.ChangesMode == LocalChangesAction.Merge;
        StashRadio.IsChecked = dialog.ChangesMode == LocalChangesAction.Stash;
        ResetRadio.IsChecked = dialog.ChangesMode == LocalChangesAction.Reset;
        _showing = false;
        ShowBranches();

        RemoteBranchRadio.IsCheckedChanged += (_, _) =>
        {
            if (!_showing)
            {
                _dialog.SetRemote(RemoteBranchRadio.IsChecked == true);
                ShowBranches();
            }
        };
        BranchesBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == ComboBox.TextProperty && !_showing)
            {
                _dialog.Branch = BranchesBox.Text ?? "";
                ShowBranch();
            }
        };
        ResetBranchRadio.IsCheckedChanged += (_, _) => ReadNewBranchMode();
        CreateBranchRadio.IsCheckedChanged += (_, _) => ReadNewBranchMode();
        DontCreateRadio.IsCheckedChanged += (_, _) => ReadNewBranchMode();
        CustomBranchNameBox.LostFocus += (_, _) => NormaliseCustomName();
        foreach (RadioButton radio in new[] { DontChangeRadio, MergeRadio, StashRadio, ResetRadio })
        {
            radio.IsCheckedChanged += (_, _) => ReadChangesMode();
        }

        SetAsDefaultCheck.IsCheckedChanged += (_, _) => _dialog.SetAsDefault = SetAsDefaultCheck.IsChecked == true;
        CheckoutButton.Click += (_, _) => Run(CheckoutAsync);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(false);
            }
        };
        Opened += (_, _) => BranchesBox.Focus();
    }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public CheckoutBranchDialog Dialog => _dialog;

    /// <summary>
    ///  Normalises the custom branch name when "Auto normalise branch name" is on, as upstream's
    ///  <c>txtCustomBranchName_Leave</c>; set by the flow.
    /// </summary>
    public Func<string, string> NormaliseName { get; init; } = name => name;

    private void ShowBranches()
    {
        _showing = true;
        RemoteOptions.IsVisible = _dialog.IsRemote;
        BranchesBox.ItemsSource = _dialog.Branches;
        BranchesBox.Text = _dialog.Branch;
        _showing = false;
        ShowBranch();
    }

    // Upstream's Branches_SelectedIndexChanged: the local branch names and the count of commits against HEAD.
    private void ShowBranch()
    {
        ResetBranchText.Text = UpstreamTranslation.ByText(_dialog.ResetOptionText).Replace('&', '_');
        LocalBranchNameText.Text = $"'{_dialog.LocalBranchName}'";
        CustomBranchNameBox.Text = _dialog.CustomBranchName;
        ChangesText.Text = "";
        string branch = _dialog.Branch.Trim();
        if (branch.Length == 0)
        {
            return;
        }

        int version = ++_countVersion;
        UiActions.Run(async () =>
        {
            string text = await Task.Run(() =>
            {
                ObjectId current = _module.GetCurrentCheckout();
                return current.IsZero ? "" : _module.GetCommitCountString(current, branch);
            });
            if (version == _countVersion)
            {
                ChangesText.Text = text;
            }
        }, _ => { });
    }

    private void ReadNewBranchMode()
    {
        if (_showing)
        {
            return;
        }

        _dialog.NewBranchMode = CreateBranchRadio.IsChecked == true ? CheckoutNewBranchMode.Create
            : DontCreateRadio.IsChecked == true ? CheckoutNewBranchMode.DontCreate
            : CheckoutNewBranchMode.Reset;
        CustomBranchNameBox.IsEnabled = _dialog.NewBranchMode == CheckoutNewBranchMode.Create;
        if (CustomBranchNameBox.IsEnabled)
        {
            CustomBranchNameBox.SelectAll();
        }
    }

    // Upstream's rbReset_CheckedChanged: "Set as default" is not offered for Reset.
    private void ReadChangesMode()
    {
        if (_showing)
        {
            return;
        }

        _dialog.ChangesMode = MergeRadio.IsChecked == true ? LocalChangesAction.Merge
            : StashRadio.IsChecked == true ? LocalChangesAction.Stash
            : ResetRadio.IsChecked == true ? LocalChangesAction.Reset
            : LocalChangesAction.DontChange;
        SetAsDefaultCheck.IsEnabled = _dialog.CanSetAsDefault;
        if (!_dialog.CanSetAsDefault)
        {
            SetAsDefaultCheck.IsChecked = false;
        }
    }

    private void NormaliseCustomName()
    {
        string text = CustomBranchNameBox.Text ?? "";
        string normalised = NormaliseName(text);
        if (normalised != text)
        {
            CustomBranchNameBox.Text = normalised;
        }
    }

    private async Task CheckoutAsync()
    {
        NormaliseCustomName();
        _dialog.Branch = BranchesBox.Text ?? "";
        _dialog.CustomBranchName = CustomBranchNameBox.Text ?? "";
        string? error = await Task.Run(() => _dialog.Validate(_module.CheckBranchFormat, UpstreamTranslation.ByText));
        if (error is not null)
        {
            await new MessageBoxWindow(error, Title ?? "", MessageBoxButtons.OK).ShowDialog(this);
            return;
        }

        if (await _checkout(this))
        {
            Close(true);
        }
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}
