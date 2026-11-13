using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormAddFiles</c>: the filter, Force, Show files (a dry run) and Add files, which closes
///  the window with true when git succeeded.
/// </summary>
public sealed partial class AddFilesViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormAddFiles";

    private readonly Func<string, Task<bool>>? _runGit;

    /// <param name="addFile">The filter at first; "." (everything) when null.</param>
    /// <param name="runGit">Runs git with the arguments, showing its output; true when it succeeded.</param>
    public AddFilesViewModel(IDialogHost host, string? addFile, Func<string, Task<bool>>? runGit)
        : base(host)
    {
        _runGit = runGit;
        Filter = addFile ?? ".";
    }

    [ObservableProperty]
    public partial string Filter { get; set; }

    [ObservableProperty]
    public partial bool Force { get; set; }

    [RelayCommand]
    private Task ShowFilesAsync() => GuardAsync(() => RunAsync(dryRun: true));

    [RelayCommand]
    private Task AddFilesAsync() => GuardAsync(async () =>
    {
        if (await RunAsync(dryRun: false))
        {
            Host.Close(true);
        }
    });

    private Task<bool> RunAsync(bool dryRun)
        => _runGit?.Invoke(ApplyPatchDialog.AddFilesArguments(Filter, Force, dryRun)) ?? Task.FromResult(false);
}
