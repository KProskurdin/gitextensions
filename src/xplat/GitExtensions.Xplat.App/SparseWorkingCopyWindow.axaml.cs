using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using Sparse = GitExtensions.Xplat.Core.Repository.SparseWorkingCopy;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormSparseWorkingCopy</c> (Repository > Sparse Working Copy), bound to
///  <see cref="SparseWorkingCopyViewModel"/> (model in <see cref="Sparse"/>): turning git's sparse checkout on or off for
///  the repository and editing its rules; saving writes both and refreshes the working copy, and closing with changes asks
///  whether to save them.
/// </summary>
public partial class SparseWorkingCopyWindow : Window
{
    public SparseWorkingCopyWindow()
        : this("", _ => Task.FromResult(false))
    {
    }

    /// <param name="repositoryPath">The repository whose sparse checkout is edited.</param>
    /// <param name="runGitWithOutput">Runs the refresh command with its output shown (upstream's FormRemoteProcess).</param>
    public SparseWorkingCopyWindow(string repositoryPath, Func<string, Task<bool>> runGitWithOutput)
    {
        ViewModel = new SparseWorkingCopyViewModel(new WindowDialogHost(this),
            new Sparse(GitModules.Open(repositoryPath)), runGitWithOutput);
        DataContext = ViewModel;
        InitializeComponent();
        SetTexts();
        InitialFocus.Attach(this, () => ViewModel.Sparse.IsSparseCheckoutEnabled ? RulesEditor : EnableButton);

        // View concerns: as upstream's, the editor takes Enter, so Ctrl+Enter (Cmd+Enter on macOS) saves; and a close with
        // changes waits for upstream's question, which closes the window again once answered.
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter && e.KeyModifiers == Hotkeys.CommandModifier)
            {
                e.Handled = true;
                ViewModel.SaveCommand.Execute(null);
            }
        }, RoutingStrategies.Tunnel);
        Closing += (sender, e) =>
        {
            if (!ViewModel.MayClose)
            {
                e.Cancel = true;
                _ = ViewModel.AskToCloseAsync();
            }
        };
        Opened += (_, _) => _ = ViewModel.LoadRulesAsync();
    }

    public SparseWorkingCopyViewModel ViewModel { get; }

    /// <summary>
    ///  Completes when the rules file has been read, for tests.
    /// </summary>
    public Task RulesLoaded => ViewModel.RulesLoaded;

    /// <summary>
    ///  Completes when the last save has finished, for tests.
    /// </summary>
    public Task Saved => ViewModel.Saved;

    // Upstream builds the form in code, so its texts are TranslationStrings, translated here by text.
    private void SetTexts()
    {
        Title = TitleLabel.Text = UpstreamTranslation.ByText(Sparse.SparseWorkingCopyTitle);
        HeaderDetailsLabel.Text = UpstreamTranslation.ByText(Sparse.HeaderDetailsText);
        DisabledLabel.Text =
            UpstreamTranslation.ByText(Sparse.SparseWorkingCopySupportHasNotBeenEnabledForThisRepository);
        EnableButton.Content = UpstreamTranslation.WithAccessKey(UpstreamTranslation.ByText(Sparse.Enable));
        ToolTip.SetTip(EnableButton, string.Format(
            UpstreamTranslation.ByText(Sparse.SetsTheGitPropertyToTrueForTheLocalRepository),
            Sparse.SettingCoreSparseCheckout));
        EnabledLabel.Text = UpstreamTranslation.ByText(Sparse.SparseWorkingCopySupportIsEnabled) + ' ';
        DisableLink.Content = UpstreamTranslation.ByText(Sparse.DisableForThisRepository);
        string disableTip = string.Format(
            UpstreamTranslation.ByText(Sparse.SetsTheGitPropertyToFalseForTheLocalRepository),
            Sparse.SettingCoreSparseCheckout);
        ToolTip.SetTip(EnabledPanel, disableTip);
        RulesLabel1.Text = UpstreamTranslation.ByText(Sparse.SpecifyTheRulesForIncludingOrExcludingFilesAndDirectories);
        RulesLabel2.Text =
            UpstreamTranslation.ByText(Sparse.SpecifyTheRulesForIncludingOrExcludingFilesAndDirectoriesLine2);
        ToolTip.SetTip(RulesEditor, UpstreamTranslation.ByText(Sparse.EditsTheContentsOfTheGitInfoSparseCheckoutFile));
        RefreshCheck.Content = UpstreamTranslation.ByText(Sparse.RefreshWorkingCopyUsingTheCurrentSettingsAndRules);
        ToolTip.SetTip(RefreshCheck, string.Format(UpstreamTranslation.ByText(Sparse.RefreshWorkingCopyCheckboxHint),
            Sparse.RefreshWorkingCopyCommandName));
        SaveButton.Content = UpstreamTranslation.WithAccessKey(UpstreamTranslation.ByText(Sparse.Save));
        CancelButton.Content = UpstreamTranslation.ByText(Sparse.Cancel);
    }
}
