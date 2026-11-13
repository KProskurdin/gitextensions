using Avalonia.Controls;

namespace GitExtensions.Xplat.App;

public partial class ErrorWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
    public ErrorWindow() => InitializeComponent();

    public ErrorWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
        OkButton.Click += (_, _) => Close();
    }
}
