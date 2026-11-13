using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitExtUtils.GitUI.Theming;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormResetAnotherBranch</c>, bound to <see cref="ResetAnotherBranchViewModel"/> (rules in
///  <see cref="ResetAnotherBranchDialog"/>): moves a local branch that is not checked out to a commit, when it fast-forwards
///  or when forced, and checks it out when asked. Closes with true after the reset.
/// </summary>
public partial class ResetAnotherBranchWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public ResetAnotherBranchWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="repositoryPath">The repository.</param>
    /// <param name="revision">The commit to reset a branch to.</param>
    /// <param name="preferences">Holds "Checkout branch after reset".</param>
    /// <param name="runGit">Runs a git command with its output shown; true when it succeeded.</param>
    /// <param name="checkout">Upstream's <c>StartCheckoutBranch</c> for the branch that was reset.</param>
    public ResetAnotherBranchWindow(string repositoryPath, string revision, IAppPreferences preferences,
        Func<Window, ArgumentString, Task<bool>> runGit, Func<Window, string, Task> checkout)
    {
        ViewModel = new ResetAnotherBranchViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath), revision,
            preferences, arguments => runGit(this, arguments), branch => checkout(this, branch));
        DataContext = ViewModel;
        Resources["ResetCoralBrush"] = Coral();
        InitializeComponent();
        lblResetBranchWarning.Text = ResetAnotherBranchDialog.Warning.ReplaceLineEndings("\n");
        UpstreamTranslation.Apply(this, ResetAnotherBranchViewModel.Category);
        InitialFocus.Attach(this, () => Branches);
        Branches.KeyUp += OnBranchesKeyUp;
        Opened += (_, _) => UiActions.Run(ShowAsync, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
    }

    public ResetAnotherBranchViewModel ViewModel { get; }

    /// <summary>
    ///  Completes when the branches and the commit's summary are shown, for tests.
    /// </summary>
    public Task BranchesLoaded => ViewModel.Loaded;

    /// <summary>
    ///  Completes when the last fast-forward check has finished, for tests.
    /// </summary>
    public Task Validated => ViewModel.Validated;

    // The list opens at once when no branch is proposed (upstream's Application_Idle).
    private async Task ShowAsync()
    {
        await ViewModel.LoadAsync();
        if (string.IsNullOrEmpty(ViewModel.BranchText) && ViewModel.BranchNames.Count > 0)
        {
            Dispatcher.UIThread.Post(() => Branches.IsDropDownOpen = true, DispatcherPriority.Background);
        }
    }

    // Upstream's Branches_KeyUp: typing opens the list, keeping what was typed.
    private void OnBranchesKeyUp(object? sender, KeyEventArgs e)
    {
        if (Branches.IsDropDownOpen || e.Key is Key.LeftAlt or Key.RightAlt or Key.Enter or Key.Escape or Key.Tab)
        {
            return;
        }

        string? text = Branches.Text;
        Branches.IsDropDownOpen = true;
        if (Branches.Text != text)
        {
            Branches.Text = text;
        }
    }

    // Upstream's Color.LightCoral.AdaptBackColor().
    private static SolidColorBrush Coral()
    {
        System.Drawing.Color coral = System.Drawing.Color.LightCoral.AdaptBackColor();
        return new SolidColorBrush(Color.FromRgb(coral.R, coral.G, coral.B));
    }
}
