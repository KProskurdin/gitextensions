using Avalonia.Controls;
using GitExtensions.Xplat.Core.Diff;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's "Show all available difftools" (<c>difftools.showavailable</c>): beside "Open in diff tool", a menu of every
///  diff tool git can start here, read when it opens (<see cref="DiffToolChoicesViewModel"/>).
/// </summary>
internal static class DiffToolChoices
{
    /// <param name="button">The menu button; shown only while the setting is on.</param>
    /// <param name="open">Opens the file in the chosen tool.</param>
    /// <param name="showError">Shows a failure to the user.</param>
    public static void Attach(Button button, Func<string, Task> open, Action<string> showError)
    {
        DiffToolChoicesViewModel viewModel = new(() => AppServices.DiffMergeTools.GetAvailableAsync(diff: true), open,
            showError);

        // Built before it opens: Avalonia shows a menu whose items change while it opens as a thin line.
        PopulatingMenuFlyout flyout = new()
        {
            ReportError = ex => showError(ex.Message),
        };
        flyout.PopulateAsync = async () => MenuEntries.Fill(flyout.Items, await viewModel.EntriesAsync(), button);
        button.Flyout = flyout;
        Show(button);
    }

    /// <summary>
    ///  Shows or hides the menu button for the setting, e.g. after the settings were saved.
    /// </summary>
    public static void Show(Button button) => button.IsVisible = AppServices.Preferences.ShowAvailableDiffTools;
}
