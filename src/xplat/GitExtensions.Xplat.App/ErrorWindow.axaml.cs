using Avalonia.Controls;

namespace GitExtensions.Xplat.App;

public partial class ErrorWindow : Window
{
    public ErrorWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
        OkButton.Click += (_, _) => Close();
    }
}
