using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Asks for a name, such as a new branch or tag name, bound to <see cref="PromptViewModel"/>. Closes with a
///  <see cref="PromptResult"/>, or with null when cancelled or when the name is empty.
/// </summary>
public partial class PromptWindow : Avalonia.Controls.Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
    public PromptWindow() => InitializeComponent();

    /// <param name="allowEmpty">Accept an empty value (a script's user input may be empty, as upstream's prompt allows).</param>
    public PromptWindow(string title, string label, string acceptText, string secondPlaceholder = "",
        string initialValue = "",
        bool allowEmpty = false)
    {
        DataContext = new PromptViewModel(new WindowDialogHost(this), title, label, acceptText, secondPlaceholder,
            initialValue, allowEmpty);
        InitializeComponent();
        Opened += (_, _) => ValueBox.Focus();
    }
}
