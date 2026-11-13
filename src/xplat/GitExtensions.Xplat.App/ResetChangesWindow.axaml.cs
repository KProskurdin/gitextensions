using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormResetChanges</c>, bound to <see cref="ResetChangesViewModel"/>: asks before changes are reset, and
///  whether new files and directories go too.
/// </summary>
public partial class ResetChangesWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
    public ResetChangesWindow() => InitializeComponent();

    /// <param name="hasExistingFiles">Whether changed tracked files are among those reset.</param>
    /// <param name="hasNewFiles">Whether new (untracked) files are among those reset.</param>
    /// <param name="confirmationMessage">Upstream's question in place of the default one, as the file lists ask it.</param>
    public ResetChangesWindow(bool hasExistingFiles, bool hasNewFiles, string? confirmationMessage = null)
    {
        DataContext = new ResetChangesViewModel(new WindowDialogHost(this), hasExistingFiles, hasNewFiles,
            confirmationMessage);
        InitializeComponent();
        InitialFocus.Attach(this);
    }

    /// <summary>
    ///  Upstream's <c>FormResetChanges.ShowResetDialog</c>.
    /// </summary>
    public static async Task<ResetChangesAction> ShowResetDialogAsync(Window owner, bool hasExistingFiles, bool hasNewFiles,
        string? confirmationMessage = null)
        => await new ResetChangesWindow(hasExistingFiles, hasNewFiles, confirmationMessage)
               .ShowDialog<ResetChangesAction?>(owner)
           ?? ResetChangesAction.Cancel;
}
