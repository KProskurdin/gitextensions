using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using Sparse = GitExtensions.Xplat.Core.Repository.SparseWorkingCopy;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormSparseWorkingCopy</c> (Repository > Sparse Working Copy; model in <see cref="Sparse"/>): turning
///  git's sparse checkout on or off for the repository and editing its rules; saving writes both and refreshes the
///  working copy, and closing with changes asks whether to save them.
/// </summary>
public partial class SparseWorkingCopyWindow : Window
{
    private readonly Sparse _sparse;
    private readonly Func<string, Task<bool>> _runGitWithOutput;
    private readonly TaskCompletionSource _rulesLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _closeDecided;

    public SparseWorkingCopyWindow()
        : this("", _ => Task.FromResult(false))
    {
    }

    /// <param name="repositoryPath">The repository whose sparse checkout is edited.</param>
    /// <param name="runGitWithOutput">Runs the refresh command with its output shown (upstream's FormRemoteProcess).</param>
    public SparseWorkingCopyWindow(string repositoryPath, Func<string, Task<bool>> runGitWithOutput)
    {
        _sparse = new Sparse(GitModules.Open(repositoryPath));
        _runGitWithOutput = runGitWithOutput;
        InitializeComponent();
        SetTexts();
        InitialFocus.Attach(this, () => _sparse.IsSparseCheckoutEnabled ? RulesEditor : EnableButton);
        RefreshCheck.IsChecked = _sparse.IsRefreshWorkingCopyOnSave;
        RefreshCheck.IsCheckedChanged += (_, _) => _sparse.IsRefreshWorkingCopyOnSave = RefreshCheck.IsChecked == true;
        EnableButton.Click += (_, _) => _sparse.IsSparseCheckoutEnabled = true;
        DisableLink.Click += (_, _) => _sparse.IsSparseCheckoutEnabled = false;
        RulesEditor.TextChanged += (_, _) => _sparse.RulesText = RulesEditor.Text ?? "";
        SaveButton.Click += (_, _) => Run(SaveAndCloseAsync);
        CancelButton.Click += (_, _) => Close();
        _sparse.PropertyChanged += (_, _) => ShowState();
        ShowState();

        // As upstream's: the editor takes Enter, so Ctrl+Enter saves.
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control)
            {
                e.Handled = true;
                Run(SaveAndCloseAsync);
            }
        }, RoutingStrategies.Tunnel);
        Closing += OnClosing;
        Opened += (_, _) => Run(LoadRulesAsync);
    }

    /// <summary>
    ///  Completes when the rules file has been read, for tests.
    /// </summary>
    public Task RulesLoaded => _rulesLoaded.Task;

    /// <summary>
    ///  Completes when the last save has finished, for tests.
    /// </summary>
    public Task Saved { get; private set; } = Task.CompletedTask;

    private static string Caption(string action)
        => $"{UpstreamTranslation.ByText(Sparse.SparseWorkingCopyTitle)} – {UpstreamTranslation.ByText(action)}";

    private void SetTexts()
    {
        Title = TitleLabel.Text = UpstreamTranslation.ByText(Sparse.SparseWorkingCopyTitle);
        HeaderDetailsLabel.Text = UpstreamTranslation.ByText(Sparse.HeaderDetailsText);
        DisabledLabel.Text = UpstreamTranslation.ByText(Sparse.SparseWorkingCopySupportHasNotBeenEnabledForThisRepository);
        EnableButton.Content = UpstreamTranslation.WithAccessKey(UpstreamTranslation.ByText(Sparse.Enable));
        ToolTip.SetTip(EnableButton, string.Format(UpstreamTranslation.ByText(Sparse.SetsTheGitPropertyToTrueForTheLocalRepository),
            Sparse.SettingCoreSparseCheckout));
        EnabledLabel.Text = UpstreamTranslation.ByText(Sparse.SparseWorkingCopySupportIsEnabled) + ' ';
        DisableLink.Content = UpstreamTranslation.ByText(Sparse.DisableForThisRepository);
        string disableTip = string.Format(UpstreamTranslation.ByText(Sparse.SetsTheGitPropertyToFalseForTheLocalRepository),
            Sparse.SettingCoreSparseCheckout);
        ToolTip.SetTip(EnabledPanel, disableTip);
        RulesLabel1.Text = UpstreamTranslation.ByText(Sparse.SpecifyTheRulesForIncludingOrExcludingFilesAndDirectories);
        RulesLabel2.Text = UpstreamTranslation.ByText(Sparse.SpecifyTheRulesForIncludingOrExcludingFilesAndDirectoriesLine2);
        ToolTip.SetTip(RulesEditor, UpstreamTranslation.ByText(Sparse.EditsTheContentsOfTheGitInfoSparseCheckoutFile));
        RefreshCheck.Content = UpstreamTranslation.ByText(Sparse.RefreshWorkingCopyUsingTheCurrentSettingsAndRules);
        ToolTip.SetTip(RefreshCheck, string.Format(UpstreamTranslation.ByText(Sparse.RefreshWorkingCopyCheckboxHint),
            Sparse.RefreshWorkingCopyCommandName));
        SaveButton.Content = UpstreamTranslation.WithAccessKey(UpstreamTranslation.ByText(Sparse.Save));
        CancelButton.Content = UpstreamTranslation.ByText(Sparse.Cancel);
    }

    // Upstream's PropertyChanged handlers: the info strip with Enable while off, the link and the rules while on.
    private void ShowState()
    {
        bool enabled = _sparse.IsSparseCheckoutEnabled;
        DisabledPanel.IsVisible = DisabledSeparator.IsVisible = !enabled;
        EnabledPanel.IsVisible = RulesPanel.IsVisible = enabled;
    }

    // Upstream's CreateViewRules: the file's text in the editor; a file that cannot be read is reported.
    private async Task LoadRulesAsync()
    {
        try
        {
            if (await Task.Run(_sparse.LoadRules) is { } text)
            {
                RulesEditor.Text = text;
                _sparse.RulesText = text;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await ShowMessageAsync($"{UpstreamTranslation.ByText(Sparse.CannotLoadTheTextOfTheSparseFile)}\n\n{ex.Message}",
                Caption(Sparse.LoadFile));
        }
        finally
        {
            _rulesLoaded.TrySetResult();
        }
    }

    // Upstream saves on OK even without changes, so the working copy is refreshed when asked; a failure is reported and
    // the window closes, as upstream's.
    private async Task SaveAndCloseAsync()
    {
        await SaveAsync();
        CloseDecided(saved: true);
    }

    private Task SaveAsync() => Saved = SaveCoreAsync();

    private async Task SaveCoreAsync()
    {
        try
        {
            await _sparse.SaveChangesAsync(ConfirmAdjustingRulesAsync, command => _runGitWithOutput(command));
            if (RulesEditor.Text != _sparse.RulesText && _sparse.RulesText is { } rules)
            {
                RulesEditor.Text = rules;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await ShowMessageAsync($"{UpstreamTranslation.ByText(Sparse.CouldNotSave)}\n\n{ex.Message}", Caption(Sparse.SaveFile));
        }
    }

    // Upstream's ComfirmAdjustingRulesOnDeactRequested: whether to rewrite the rules so that the whole working copy passes.
    private async Task<bool> ConfirmAdjustingRulesAsync(bool isRuleSetEmpty)
    {
        string reason = UpstreamTranslation.ByText(isRuleSetEmpty
            ? Sparse.WithTheSparsePassFilterEmptyOrMissing
            : Sparse.WithSomeRulesStillInTheSparsePassFilter);
        MessageBoxWindow question = new(string.Format(UpstreamTranslation.ByText(Sparse.ConfirmDisableGitSparse), reason),
            UpstreamTranslation.ByText(Sparse.DisableGitSparse), MessageBoxButtons.YesNo);
        await question.ShowDialog(this);
        return question.Result == DialogResult.Yes;
    }

    // Upstream's BindSaveOnClose for a close without Save: with changes, Yes saves, No closes and Cancel stays.
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeDecided || !_sparse.IsWithUnsavedChanges())
        {
            return;
        }

        e.Cancel = true;
        Run(AskToSaveAsync);
    }

    private async Task AskToSaveAsync()
    {
        MessageBoxWindow question = new(
            UpstreamTranslation.ByText(Sparse.YouHaveMadeChangesToSettingsOrRulesWouldYouLikeToSaveThem),
            Caption(Sparse.Cancel), MessageBoxButtons.YesNoCancel);
        await question.ShowDialog(this);
        switch (question.Result)
        {
            case DialogResult.Yes:
                await SaveAsync();
                CloseDecided(saved: true);
                break;
            case DialogResult.No:
                CloseDecided(saved: false);
                break;
        }
    }

    private void CloseDecided(bool saved)
    {
        _closeDecided = true;
        Close(saved);
    }

    private Task ShowMessageAsync(string text, string caption)
        => new MessageBoxWindow(text, caption, MessageBoxButtons.OK).ShowDialog(this);

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}
