using Avalonia.Controls;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's "Show all available difftools" (<c>difftools.showavailable</c>): beside "Open in diff tool", a menu of every
///  diff tool git can start here, read when it opens.
/// </summary>
internal static class DiffToolChoices
{
    /// <param name="button">The menu button; shown only while the setting is on.</param>
    /// <param name="open">Opens the file in the chosen tool.</param>
    /// <param name="showError">Shows a failure to the user.</param>
    public static void Attach(Button button, Func<string, Task> open, Action<string> showError)
    {
        MenuFlyout flyout = new();
        button.Flyout = flyout;
        Show(button);
        flyout.Opening += async (_, _) =>
        {
            flyout.Items.Clear();
            IReadOnlyList<string> tools;
            try
            {
                tools = await AppServices.DiffMergeTools.GetAvailableAsync(diff: true);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                showError(ex.Message);
                return;
            }

            foreach (string tool in tools)
            {
                MenuItem item = new() { Header = new TextBlock { Text = tool } };
                item.Click += (_, _) => UiActions.Run(() => open(tool), ex => showError(ex.Message));
                flyout.Items.Add(item);
            }
        };
    }

    /// <summary>
    ///  Shows or hides the menu button for the setting, e.g. after the settings were saved.
    /// </summary>
    public static void Show(Button button) => button.IsVisible = AppServices.Preferences.ShowAvailableDiffTools;
}
