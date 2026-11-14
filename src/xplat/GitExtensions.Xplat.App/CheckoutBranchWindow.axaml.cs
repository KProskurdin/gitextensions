using System.ComponentModel;
using Avalonia.Controls;
using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormCheckoutBranch</c> in the new shell, bound to <see cref="CheckoutBranchViewModel"/> (rules in
///  <see cref="CheckoutBranchDialog"/>, the checkout in <see cref="CheckoutBranchFlow"/>): local or remote branches, for a
///  remote branch reset or create a local branch or check out the commit, and for local changes don't change, merge,
///  stash or reset. Closes with true after checking out; stays open when the checkout did not happen.
/// </summary>
public partial class CheckoutBranchWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public CheckoutBranchWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="checkout">Checks out what the dialog describes; true when it did.</param>
    public CheckoutBranchWindow(CheckoutBranchDialog dialog, GitModule module, Func<Window, Task<bool>> checkout)
    {
        ViewModel = new CheckoutBranchViewModel(new WindowDialogHost(this), dialog, module, () => checkout(this));
        DataContext = ViewModel;
        InitializeComponent();
        DialogHelp.Attach(this, HelpButton, DialogHelp.Manual("branches", "checkout-branch"));

        // View concerns: the custom name is selected when chosen and normalised when its box is left, and the branch list
        // takes the focus.
        ViewModel.PropertyChanged += OnViewModelChanged;
        CustomBranchNameBox.LostFocus += (_, _) => ViewModel.NormaliseCustomName();
        Opened += (_, _) => BranchesBox.Focus();
    }

    public CheckoutBranchViewModel ViewModel { get; }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public CheckoutBranchDialog Dialog => ViewModel.Dialog;

    /// <summary>
    ///  Normalises the custom branch name when "Auto normalise branch name" is on, as upstream's
    ///  <c>txtCustomBranchName_Leave</c>; set by the flow.
    /// </summary>
    public Func<string, string> NormaliseName
    {
        get => ViewModel.NormaliseName;
        init => ViewModel.NormaliseName = value;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CheckoutBranchViewModel.UsesCustomName) && ViewModel.UsesCustomName)
        {
            CustomBranchNameBox.SelectAll();
        }
    }
}
