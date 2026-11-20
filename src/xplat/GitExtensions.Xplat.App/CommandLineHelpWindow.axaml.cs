using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormCommandlineHelp</c>, bound to <see cref="CommandLineHelpViewModel"/>.
/// </summary>
public partial class CommandLineHelpWindow : Window
{
    public CommandLineHelpWindow()
    {
        DataContext = new CommandLineHelpViewModel();
        InitializeComponent();
        UpstreamTranslation.Apply(this, CommandLineHelpViewModel.Category);
    }
}
