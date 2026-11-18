using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  Upstream's "Show all available difftools" (<c>difftools.showavailable</c>): beside "Open in diff tool", a menu of every
///  diff tool git can start here, read when it opens.
/// </summary>
/// <param name="readTools">Reads the diff tools git can start.</param>
/// <param name="open">Opens the file in the chosen tool.</param>
/// <param name="showError">Shows a failure to the user.</param>
public sealed partial class DiffToolChoicesViewModel(Func<Task<IReadOnlyList<string>>> readTools,
    Func<string, Task> open, Action<string> showError)
{
    /// <summary>
    ///  The menu's items, a tool each.
    /// </summary>
    public async Task<IReadOnlyList<MenuEntry>> EntriesAsync()
        => [.. (await readTools()).Select(tool => new MenuEntry(tool, OpenCommand, tool))];

    [RelayCommand]
    private async Task OpenAsync(string tool)
    {
        try
        {
            await open(tool);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            showError(ex.Message);
        }
    }
}
