using CommunityToolkit.Mvvm.ComponentModel;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Platform;

/// <summary>
///  The view model of upstream's <c>FormChangeLog</c>: the change log upstream ships, read-only.
/// </summary>
public sealed partial class ChangeLogViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormChangeLog";

    private readonly Func<Stream> _open;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <param name="open">Opens the change log the app keeps (an app asset).</param>
    public ChangeLogViewModel(IDialogHost host, Func<Stream> open)
        : base(host)
    {
        _open = open;
        Text = "";
    }

    [ObservableProperty]
    public partial string Text { get; private set; }

    /// <summary>
    ///  Completes when the change log is shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Reads the change log. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        try
        {
            await using Stream stream = _open();
            using StreamReader reader = new(stream, detectEncodingFromByteOrderMarks: true);
            Text = await reader.ReadToEndAsync();
        }
        finally
        {
            _loaded.TrySetResult();
        }
    });
}
