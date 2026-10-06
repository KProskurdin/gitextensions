using Avalonia.Controls;
using GitCommands;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Asks what a checkout does with local changes: the new shell's version of that part of upstream
///  <c>FormCheckoutBranch</c>. Closes with the choice, or null when cancelled.
/// </summary>
public partial class CheckoutWindow : Window
{
    public CheckoutWindow(string branch, int changes, LocalChangesAction defaultAction)
    {
        InitializeComponent();
        MessageText.Text = $"There are local changes in {changes} file(s). What should happen to them when {branch} is checked out?";
        KeepRadio.IsChecked = defaultAction == LocalChangesAction.DontChange;
        MergeRadio.IsChecked = defaultAction == LocalChangesAction.Merge;
        StashRadio.IsChecked = defaultAction == LocalChangesAction.Stash;
        ResetRadio.IsChecked = defaultAction == LocalChangesAction.Reset;
        CancelButton.Click += (_, _) => Close(null);
        CheckoutButton.Click += (_, _) => Close(Choice());
    }

    private LocalChangesAction Choice()
        => MergeRadio.IsChecked == true ? LocalChangesAction.Merge
            : StashRadio.IsChecked == true ? LocalChangesAction.Stash
            : ResetRadio.IsChecked == true ? LocalChangesAction.Reset
            : LocalChangesAction.DontChange;
}
