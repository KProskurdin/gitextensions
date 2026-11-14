using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The view model of upstream's <c>FormSparseWorkingCopy</c> (Repository > Sparse Working Copy; model in
///  <see cref="SparseWorkingCopy"/>): turning git's sparse checkout on or off for the repository and editing its rules;
///  saving writes both and refreshes the working copy, and closing with changes asks whether to save them
///  (<see cref="MayClose"/>, <see cref="AskToCloseAsync"/>). Closes its window with true when saved.
/// </summary>
public sealed partial class SparseWorkingCopyViewModel : DialogViewModel
{
    private readonly Func<string, Task<bool>> _runGitWithOutput;
    private readonly TaskCompletionSource _rulesLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _closeDecided;

    /// <param name="sparse">The repository's sparse checkout settings and rules.</param>
    /// <param name="runGitWithOutput">Runs the refresh command with its output shown (upstream's FormRemoteProcess).</param>
    public SparseWorkingCopyViewModel(IDialogHost host, SparseWorkingCopy sparse, Func<string, Task<bool>> runGitWithOutput)
        : base(host)
    {
        Sparse = sparse;
        _runGitWithOutput = runGitWithOutput;
    }

    /// <summary>
    ///  The settings and rules the window binds to: the on/off state, the rules text and "Refresh working copy".
    /// </summary>
    public SparseWorkingCopy Sparse { get; }

    /// <summary>
    ///  Completes when the rules file has been read, for tests.
    /// </summary>
    public Task RulesLoaded => _rulesLoaded.Task;

    /// <summary>
    ///  Completes when the last save has finished, for tests.
    /// </summary>
    public Task Saved { get; private set; } = Task.CompletedTask;

    /// <summary>
    ///  True when the window may close without a question: nothing unsaved, or the question was answered.
    /// </summary>
    public bool MayClose => _closeDecided || !Sparse.IsWithUnsavedChanges();

    /// <summary>
    ///  Upstream's <c>CreateViewRules</c>: the file's text in the editor; a file that cannot be read is reported. The window
    ///  calls it when it opens.
    /// </summary>
    public Task LoadRulesAsync() => GuardAsync(async () =>
    {
        try
        {
            if (await Task.Run(Sparse.LoadRules) is { } text)
            {
                Sparse.RulesText = text;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Host.ShowMessageAsync($"{Host.Translate(SparseWorkingCopy.CannotLoadTheTextOfTheSparseFile)}\n\n{ex.Message}",
                Caption(SparseWorkingCopy.LoadFile));
        }
        finally
        {
            _rulesLoaded.TrySetResult();
        }
    });

    /// <summary>
    ///  Upstream's <c>BindSaveOnClose</c> for a close without Save: with changes, Yes saves, No closes and Cancel stays.
    /// </summary>
    public Task AskToCloseAsync() => GuardAsync(async () =>
    {
        switch (await Host.AskAsync(
                    Host.Translate(SparseWorkingCopy.YouHaveMadeChangesToSettingsOrRulesWouldYouLikeToSaveThem),
                    Caption(SparseWorkingCopy.Cancel)))
        {
            case true:
                await SaveChangesAsync();
                CloseDecided(saved: true);
                break;
            case false:
                CloseDecided(saved: false);
                break;
        }
    });

    [RelayCommand]
    private void Enable() => Sparse.IsSparseCheckoutEnabled = true;

    [RelayCommand]
    private void Disable() => Sparse.IsSparseCheckoutEnabled = false;

    // Upstream saves on OK even without changes, so the working copy is refreshed when asked; a failure is reported and
    // the window closes, as upstream's.
    [RelayCommand]
    private Task SaveAsync() => GuardAsync(async () =>
    {
        await SaveChangesAsync();
        CloseDecided(saved: true);
    });

    [RelayCommand]
    private void Cancel() => Host.Close(null);

    private string Caption(string action)
        => $"{Host.Translate(SparseWorkingCopy.SparseWorkingCopyTitle)} – {Host.Translate(action)}";

    private Task SaveChangesAsync() => Saved = SaveCoreAsync();

    private async Task SaveCoreAsync()
    {
        try
        {
            await Sparse.SaveChangesAsync(ConfirmAdjustingRulesAsync, command => _runGitWithOutput(command));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await Host.ShowMessageAsync($"{Host.Translate(SparseWorkingCopy.CouldNotSave)}\n\n{ex.Message}",
                Caption(SparseWorkingCopy.SaveFile));
        }
    }

    // Upstream's ComfirmAdjustingRulesOnDeactRequested: whether to rewrite the rules so that the whole working copy passes.
    private Task<bool> ConfirmAdjustingRulesAsync(bool isRuleSetEmpty)
    {
        string reason = Host.Translate(isRuleSetEmpty
            ? SparseWorkingCopy.WithTheSparsePassFilterEmptyOrMissing
            : SparseWorkingCopy.WithSomeRulesStillInTheSparsePassFilter);
        return Host.ConfirmAsync(string.Format(Host.Translate(SparseWorkingCopy.ConfirmDisableGitSparse), reason),
            Host.Translate(SparseWorkingCopy.DisableGitSparse));
    }

    private void CloseDecided(bool saved)
    {
        _closeDecided = true;
        Host.Close(saved);
    }
}
