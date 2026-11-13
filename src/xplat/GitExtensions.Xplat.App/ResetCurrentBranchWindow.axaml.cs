using Avalonia.Controls;
using Avalonia.Media;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtUtils.GitUI.Theming;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormResetCurrentBranch</c> in the new shell, bound to <see cref="ResetCurrentBranchViewModel"/> (rules in
///  <see cref="ResetCurrentBranchDialog"/>): resets the current branch to a commit, soft, mixed, keep, merge or hard (after
///  a question), and updates the submodules when upstream does. Closes with true after resetting.
/// </summary>
public partial class ResetCurrentBranchWindow : Window
{
    // Upstream's OtherColors.BrightGreen, BrightYellow and BrightRed behind the reset types, dimmed in dark mode.
    private static readonly System.Drawing.Color _soft = System.Drawing.Color.FromArgb(128, 255, 128);
    private static readonly System.Drawing.Color _mixed = System.Drawing.Color.FromArgb(255, 255, 128);
    private static readonly System.Drawing.Color _hard = System.Drawing.Color.FromArgb(255, 128, 128);

    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public ResetCurrentBranchWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="target">The commit to reset to (a hash or ref).</param>
    /// <param name="mode">Upstream's reset type to start with.</param>
    /// <param name="updateSubmodules">Upstream's <c>UpdateSubmodules</c> after a reset that moved HEAD.</param>
    public ResetCurrentBranchWindow(string repositoryPath, RepositoryOperationsViewModel actions,
        IAppPreferences preferences, string target, ResetMode mode, Func<string, Task<bool>> updateSubmodules)
    {
        ViewModel = new ResetCurrentBranchViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath), actions,
            preferences, target, mode, updateSubmodules);
        DataContext = ViewModel;
        InitializeComponent();
        DialogHelp.Attach(this, HelpButton, () => ViewModel.Dialog.HelpUrl());
        Dictionary<ResetMode, RadioButton> radios = new()
        {
            [ResetMode.Soft] = SoftRadio,
            [ResetMode.Mixed] = MixedRadio,
            [ResetMode.Keep] = KeepRadio,
            [ResetMode.Merge] = MergeRadio,
            [ResetMode.Hard] = HardRadio,
        };

        // View concerns: upstream's colors behind the reset types, and the chosen type focused once loaded.
        foreach ((ResetMode radioMode, RadioButton radio) in radios)
        {
            System.Drawing.Color back = radioMode switch
            {
                ResetMode.Soft => _soft,
                ResetMode.Hard => _hard,
                _ => _mixed,
            };
            if (ThemeBrushes.Current.IsDark)
            {
                back = back.DimColor();
            }

            radio.Background = new SolidColorBrush(Color.FromRgb(back.R, back.G, back.B));
            radio.Foreground = ThemeBrushes.Current.IsDark ? Brushes.White : Brushes.Black;
        }

        Opened += (_, _) => UiActions.Run(
            async () =>
            {
                await ViewModel.LoadAsync();
                radios[ViewModel.Dialog.Mode].Focus();
            },
            ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
    }

    public ResetCurrentBranchViewModel ViewModel { get; }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public ResetCurrentBranchDialog Dialog => ViewModel.Dialog;

    /// <summary>
    ///  Completes when the branch and the commit's summary are shown, for tests.
    /// </summary>
    public Task ResetLoaded => ViewModel.Loaded;
}
