using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormAddFiles</c>, bound to <see cref="AddFilesViewModel"/>: <c>git add</c> of a filter (with <c>-f</c>
///  when forced), first as a dry run with "Show files"; git runs through the caller with its output shown, as upstream's
///  FormProcess. Add files closes the window when git succeeded.
/// </summary>
public partial class AddFilesWindow : Window
{
    public AddFilesWindow()
        : this(null, null)
    {
    }

    /// <param name="addFile">The filter at first; "." (everything) when null.</param>
    /// <param name="runGit">Runs git with the arguments, showing its output; true when it succeeded.</param>
    public AddFilesWindow(string? addFile, Func<string, Task<bool>>? runGit)
    {
        DataContext = new AddFilesViewModel(new WindowDialogHost(this), addFile, runGit);
        InitializeComponent();
        UpstreamTranslation.Apply(this, AddFilesViewModel.Category);
        InitialFocus.Attach(this, () => Filter);
    }
}
