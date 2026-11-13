using Avalonia.Controls;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.App;

/// <summary>
///  A help text in its own window, as upstream's <c>SimpleHelpDisplayDialog</c>, bound to <see cref="TextViewModel"/>;
///  shown beside its owner, not modal.
/// </summary>
public partial class HelpWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
    public HelpWindow() => InitializeComponent();

    public HelpWindow(string title, string content)
    {
        DataContext = new TextViewModel(new WindowDialogHost(this), title, content);
        InitializeComponent();
    }
}
