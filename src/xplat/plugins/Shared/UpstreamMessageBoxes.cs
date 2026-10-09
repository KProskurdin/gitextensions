namespace GitUI;

/// <summary>
///  Stands in for the members of GitUI's <c>MessageBoxes</c> that upstream plugin classes use (Gource, Azure DevOps, GitHub).
///  GitUI is the WinForms project, which the new shell does not build; these forward to the shared
///  <see cref="GitExtensions.Extensibility.MessageBoxes"/>, which GitUI's own class also wraps. Linked into each plugin that
///  needs it, so it stays internal to each.
/// </summary>
internal static class MessageBoxes
{
    public static void ShowError(IWin32Window? owner, string text, string? caption = null)
        => GitExtensions.Extensibility.MessageBoxes.ShowError(owner, text, caption);

    public static DialogResult Show(IWin32Window? owner, string text, string caption, MessageBoxButtons buttons,
        MessageBoxIcon icon, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        => GitExtensions.Extensibility.MessageBoxes.Show(owner, text, caption, buttons, icon, defaultButton);
}
