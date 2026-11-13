using Avalonia.Controls;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>ExceptionDetailView</c>: the property of the bug report's exception that was double-clicked, and its
///  whole value.
/// </summary>
public partial class ExceptionDetailWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
    public ExceptionDetailWindow() => InitializeComponent();

    public ExceptionDetailWindow(string property, string information)
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, "ExceptionDetailView");
        propertyTextBox.Text = property;
        propertyInformationTextBox.Text = information;
    }
}
