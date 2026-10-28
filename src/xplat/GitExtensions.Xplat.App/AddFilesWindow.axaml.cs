using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormAddFiles</c>: <c>git add</c> of a filter (with <c>-f</c> when forced), first as a dry run with
///  "Show files"; git runs through the caller with its output shown, as upstream's FormProcess. Add files closes the
///  window when git succeeded.
/// </summary>
public partial class AddFilesWindow : Window
{
    private readonly Func<string, Task<bool>>? _runGit;

    public AddFilesWindow()
        : this(null, null)
    {
    }

    /// <param name="addFile">The filter at first; "." (everything) when null.</param>
    /// <param name="runGit">Runs git with the arguments, showing its output; true when it succeeded.</param>
    public AddFilesWindow(string? addFile, Func<string, Task<bool>>? runGit)
    {
        _runGit = runGit;
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormAddFiles");
        InitialFocus.Attach(this, () => Filter);
        Filter.Text = addFile ?? ".";
        ShowFiles.Click += (_, _) => Run(() => RunAsync(dryRun: true));
        AddFiles.Click += (_, _) => Run(async () =>
        {
            if (await RunAsync(dryRun: false))
            {
                Close(true);
            }
        });
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));

    private Task<bool> RunAsync(bool dryRun)
        => _runGit?.Invoke(ApplyPatchDialog.AddFilesArguments(Filter.Text ?? "", force.IsChecked == true, dryRun))
           ?? Task.FromResult(false);
}
