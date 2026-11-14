using Avalonia.Controls;
using GitCommands;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormAvailableEncodings</c>, bound to <see cref="AvailableEncodingsViewModel"/>: which encodings the app
///  offers. It closes with true when they were changed.
/// </summary>
public partial class AvailableEncodingsWindow : Window
{
    public AvailableEncodingsWindow()
    {
        // Only SaveSettings writes the list.
        DataContext = new AvailableEncodingsViewModel(new WindowDialogHost(this), AppSettings.AvailableEncodings,
            AppSettings.SaveSettings);
        InitializeComponent();
    }
}
