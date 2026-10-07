using Avalonia.Controls;

namespace GitExtensions.Xplat.App;

/// <summary>
///  A help text in its own window, as upstream's <c>SimpleHelpDisplayDialog</c>; shown beside its owner, not modal.
/// </summary>
public partial class HelpWindow : Window
{
    public HelpWindow(string title, string content)
    {
        InitializeComponent();
        Title = title;
        ContentText.Text = content;
        CloseButton.Click += (_, _) => Close();
    }
}
