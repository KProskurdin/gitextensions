using Avalonia.Controls;

namespace GitExtensions.Xplat.App;

/// <summary>
///  A window that opens the settings window for the windows it owns: upstream's GitUICommands.StartSettingsDialog, which
///  a form's controls reach through the commands of the browse window that opened the form.
/// </summary>
public interface ISettingsWindowHost
{
    /// <summary>
    ///  Opens the settings window on <paramref name="page"/> over <paramref name="owner"/>.
    /// </summary>
    Task ShowSettingsAsync(Window owner, string page);

    /// <summary>
    ///  Finds the host of <paramref name="window"/>: the window itself or the nearest of its owners that is one.
    /// </summary>
    static ISettingsWindowHost? Of(Window? window)
    {
        for (Window? current = window; current is not null; current = current.Owner as Window)
        {
            if (current is ISettingsWindowHost host)
            {
                return host;
            }
        }

        return null;
    }
}
