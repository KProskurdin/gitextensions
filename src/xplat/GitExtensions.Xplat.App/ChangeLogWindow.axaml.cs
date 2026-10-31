using Avalonia.Controls;
using Avalonia.Platform;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormChangeLog</c> (Help > Changelog): upstream's change log, linked into the app from upstream's
///  resources, as upstream's <c>Resources.ChangeLog</c>.
/// </summary>
public partial class ChangeLogWindow : Window
{
    /// <summary>
    ///  Where the app keeps upstream's change log.
    /// </summary>
    public static readonly Uri ChangeLogUri = new("avares://GitExtensions/Assets/Upstream/ChangeLog.md");

    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ChangeLogWindow()
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormChangeLog");
        Opened += (_, _) => UiActions.Run(LoadAsync, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
    }

    /// <summary>
    ///  Completes when the change log is shown, for tests.
    /// </summary>
    public Task ChangeLogLoaded => _loaded.Task;

    private async Task LoadAsync()
    {
        try
        {
            await using Stream stream = AssetLoader.Open(ChangeLogUri);
            using StreamReader reader = new(stream, detectEncodingFromByteOrderMarks: true);
            ChangeLog.Text = await reader.ReadToEndAsync();
        }
        finally
        {
            _loaded.TrySetResult();
        }
    }
}
