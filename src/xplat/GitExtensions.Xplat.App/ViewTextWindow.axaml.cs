using Avalonia.Controls;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormEdit</c> read-only (its only use in the new shell, from the lost objects window): a text in the file
///  viewer, highlighted by its file name's type.
/// </summary>
public partial class ViewTextWindow : Window
{
    public ViewTextWindow()
        : this("", "")
    {
    }

    /// <param name="text">The text shown.</param>
    /// <param name="fileName">The name whose extension picks the highlighting.</param>
    public ViewTextWindow(string text, string fileName)
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormEdit");
        Viewer.ShowText(fileName, text);
    }
}
