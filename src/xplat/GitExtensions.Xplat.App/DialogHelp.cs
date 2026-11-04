using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitCommands;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's title bar help button (<c>GitExtensionsDialog.OnHelpButtonClicked</c>, <c>HelpButton = true</c>), which an
///  Avalonia window cannot have: F1 and a small "?" button at the left of the dialog's buttons open the dialog's page.
/// </summary>
public static class DialogHelp
{
    // Upstream's GotoUserManualControl tooltip.
    private const string Tooltip = "Read more about this feature at {0}";

    /// <summary>
    ///  Opens a page, for tests.
    /// </summary>
    public static Action<string> Open { get; set; } = OsShellUtil.OpenUrlInDefaultBrowser;

    /// <summary>
    ///  Opens the page <paramref name="url"/> gives (read when asked, as the reset window's depends on its choice) from F1
    ///  in <paramref name="window"/> and from <paramref name="button"/>, whose tooltip names it.
    /// </summary>
    public static void Attach(Window window, Button button, Func<string> url)
    {
        button.Click += (_, _) => Open(url());
        ToolTip.SetTip(button, TooltipFor(url()));
        button.PointerEntered += (_, _) => ToolTip.SetTip(button, TooltipFor(url()));
        window.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e is { Key: Key.F1, KeyModifiers: KeyModifiers.None })
            {
                e.Handled = true;
                Open(url());
            }
        }, RoutingStrategies.Tunnel);
    }

    /// <summary>
    ///  Upstream's <c>UserManual.UrlFor</c> for a dialog's <c>ManualSectionSubfolder</c> and <c>ManualSectionAnchorName</c>.
    /// </summary>
    public static Func<string> Manual(string subfolder, string anchor)
        => () => GitUI.UserManual.UserManual.UrlFor(subfolder, anchor);

    private static string TooltipFor(string url) => string.Format(UpstreamTranslation.ByText(Tooltip), url);
}
