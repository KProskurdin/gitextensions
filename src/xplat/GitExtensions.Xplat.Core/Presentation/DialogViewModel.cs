using CommunityToolkit.Mvvm.ComponentModel;

namespace GitExtensions.Xplat.Core.Presentation;

/// <summary>
///  The base of a window's view model (PLAN.md principle 6): the <see cref="IDialogHost"/> it reaches its window through,
///  and the guard its commands run in, so an error is shown instead of ending the app.
/// </summary>
public abstract class DialogViewModel : ObservableObject
{
    protected DialogViewModel(IDialogHost host)
    {
        Host = host;
    }

    protected IDialogHost Host { get; }

    /// <summary>
    ///  Runs <paramref name="action"/> and shows any error that escapes it; a cancellation is not an error (a closed window
    ///  cancels its reads). A command's body runs in this, because an exception that escapes an asynchronous command ends
    ///  the app.
    /// </summary>
    protected async Task GuardAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // Nothing to report.
        }
        catch (Exception ex)
        {
            Host.ShowError(ex);
        }
    }

    /// <summary>
    ///  Starts <paramref name="action"/> in <see cref="GuardAsync"/> without waiting for it, for work that a property
    ///  change starts.
    /// </summary>
    protected void Run(Func<Task> action) => _ = GuardAsync(action);
}
