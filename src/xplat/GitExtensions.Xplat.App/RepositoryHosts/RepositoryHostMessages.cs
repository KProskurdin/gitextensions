using Avalonia.Controls;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.RepositoryHosts;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;

namespace GitExtensions.Xplat.App.RepositoryHosts;

/// <summary>
///  The repository host windows' messages as upstream shows them: <c>MessageBoxes.Show</c> over the window, with OK and the
///  error or information icon.
/// </summary>
internal static class RepositoryHostMessages
{
    public static ShowRepositoryHostMessage For(Window window)
        => (text, caption, isError) => MessageBoxes.Show(new WindowOwner(window), text, caption, MessageBoxButtons.OK,
            isError ? MessageBoxIcon.Error : MessageBoxIcon.Information);
}
