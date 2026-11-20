using Avalonia.Controls;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The shell's error window, bound to <see cref="MessageViewModel"/>: a message and OK.
/// </summary>
public partial class ErrorWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
    public ErrorWindow() => InitializeComponent();

    /// <param name="message">The error.</param>
    /// <param name="details">Upstream's details of a failed command (<see cref="ErrorReporter.Details"/>), or empty.</param>
    public ErrorWindow(string message, string details = "")
    {
        DataContext = new MessageViewModel(new WindowDialogHost(this), message, details);
        InitializeComponent();
    }
}
