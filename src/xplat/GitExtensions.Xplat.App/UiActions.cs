namespace GitExtensions.Xplat.App;

/// <summary>
///  Starts asynchronous work from an event handler, which cannot await it.
/// </summary>
internal static class UiActions
{
    /// <summary>
    ///  Runs <paramref name="action"/> and passes any exception that escapes it to <paramref name="report"/>. An exception
    ///  that escapes an <c>async void</c> handler ends the process instead, so handlers use this.
    /// </summary>
    public static void Run(Func<Task> action, Action<Exception> report)
    {
        _ = RunAsync(action, report);
    }

    private static async Task RunAsync(Func<Task> action, Action<Exception> report)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // A closed window cancels its reads; there is nothing to report.
        }
        catch (Exception ex)
        {
            report(ex);
        }
    }
}
