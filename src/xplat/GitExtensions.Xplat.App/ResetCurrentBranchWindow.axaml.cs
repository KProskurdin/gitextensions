using Avalonia.Controls;
using Avalonia.Media;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitExtUtils.GitUI.Theming;
using GitUIPluginInterfaces;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormResetCurrentBranch</c> in the new shell (rules in <see cref="ResetCurrentBranchDialog"/>): resets the
///  current branch to a commit, soft, mixed, keep, merge or hard (after a question), and updates the submodules when
///  upstream does. Closes with true after resetting.
/// </summary>
public partial class ResetCurrentBranchWindow : Window
{
    // Upstream's OtherColors.BrightGreen, BrightYellow and BrightRed behind the reset types, dimmed in dark mode.
    private static readonly System.Drawing.Color _soft = System.Drawing.Color.FromArgb(128, 255, 128);
    private static readonly System.Drawing.Color _mixed = System.Drawing.Color.FromArgb(255, 255, 128);
    private static readonly System.Drawing.Color _hard = System.Drawing.Color.FromArgb(255, 128, 128);

    private readonly string _repositoryPath;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private readonly string _target;
    private readonly Func<string, Task<bool>> _updateSubmodules;
    private readonly GitModule _module;
    private readonly ResetCurrentBranchDialog _dialog;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Dictionary<ResetMode, RadioButton> _radios;
    private ObjectId _revision;

    /// <param name="target">The commit to reset to (a hash or ref).</param>
    /// <param name="mode">Upstream's reset type to start with.</param>
    /// <param name="updateSubmodules">Upstream's <c>UpdateSubmodules</c> after a reset that moved HEAD.</param>
    public ResetCurrentBranchWindow(string repositoryPath, RepositoryOperationsViewModel actions,
        IAppPreferences preferences, string target, ResetMode mode, Func<string, Task<bool>> updateSubmodules)
    {
        _repositoryPath = repositoryPath;
        _actions = actions;
        _preferences = preferences;
        _target = target;
        _updateSubmodules = updateSubmodules;
        _module = GitModules.Open(repositoryPath);
        _dialog = new ResetCurrentBranchDialog(mode);
        InitializeComponent();
        _radios = new Dictionary<ResetMode, RadioButton>
        {
            [ResetMode.Soft] = SoftRadio,
            [ResetMode.Mixed] = MixedRadio,
            [ResetMode.Keep] = KeepRadio,
            [ResetMode.Merge] = MergeRadio,
            [ResetMode.Hard] = HardRadio,
        };
        foreach ((ResetMode radioMode, RadioButton radio) in _radios)
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
            radio.IsChecked = radioMode == mode;
            radio.IsCheckedChanged += (_, _) =>
            {
                if (radio.IsChecked == true)
                {
                    _dialog.Mode = radioMode;
                }
            };
        }

        OkButton.Click += (_, _) => UiActions.Run(ResetAsync, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
        CancelButton.Click += (_, _) => Close(false);
        Opened += (_, _) => UiActions.Run(LoadAsync, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
    }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public ResetCurrentBranchDialog Dialog => _dialog;

    /// <summary>
    ///  Completes when the branch and the commit's summary are shown, for tests.
    /// </summary>
    public Task ResetLoaded => _loaded.Task;

    // Upstream's FormResetCurrentBranch_Load.
    private async Task LoadAsync()
    {
        (string branch, ObjectId id, GitRevision? revision) = await Task.Run(() =>
        {
            ObjectId id = _module.RevParse(_target);
            return (_module.GetSelectedBranch(), id,
                id.IsZero ? null : _module.GetRevision(id, shortFormat: true, loadRefs: true));
        });
        _revision = id;
        BranchInfoText.Text = string.Format(UpstreamTranslation.ByText(ResetCurrentBranchDialog.BranchInfo), branch);
        Summary.Show(revision is null ? null : CreateBranchDialog.Summarize(revision));
        _radios[_dialog.Mode].Focus();
        _loaded.TrySetResult();
    }

    // Upstream's Ok_Click: the reset (a hard one after the question), then the submodules when upstream updates them.
    private async Task ResetAsync()
    {
        if (_revision.IsZero)
        {
            return;
        }

        if (_dialog.AsksFirst)
        {
            MessageBoxWindow question = new(UpstreamTranslation.ByText(ResetCurrentBranchDialog.ResetHardWarning),
                UpstreamTranslation.ByText(ResetCurrentBranchDialog.ResetCaption), MessageBoxButtons.YesNo);
            await question.ShowDialog(this);
            if (question.Result != DialogResult.Yes)
            {
                return;
            }
        }

        ObjectId revision = _revision;
        (bool movesHead, bool hasSubmodules) = await Task.Run(() =>
            (_module.GetCurrentCheckout() != revision, _module.HasSubmodules()));
        bool reset = await _actions.ResetAsync(_repositoryPath, revision.ToString(), _dialog.Mode);
        if (reset && _dialog.UpdatesSubmodules(movesHead, _preferences.UpdateSubmodulesOnCheckout, hasSubmodules))
        {
            await _updateSubmodules(_repositoryPath);
        }

        Close(reset);
    }
}
