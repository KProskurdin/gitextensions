using Avalonia.Controls;
using Avalonia.Platform;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormChangeLog</c> (Help > Changelog), bound to <see cref="ChangeLogViewModel"/>: upstream's change log,
///  linked into the app from upstream's resources, as upstream's <c>Resources.ChangeLog</c>.
/// </summary>
public partial class ChangeLogWindow : Window
{
    /// <summary>
    ///  Where the app keeps upstream's change log.
    /// </summary>
    public static readonly Uri ChangeLogUri = new("avares://GitExtensions/Assets/Upstream/ChangeLog.md");

    public ChangeLogWindow()
    {
        ViewModel = new ChangeLogViewModel(new WindowDialogHost(this), () => AssetLoader.Open(ChangeLogUri));
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, ChangeLogViewModel.Category);
        Opened += (_, _) => UiActions.Run(ViewModel.LoadAsync, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
    }

    public ChangeLogViewModel ViewModel { get; }

    /// <summary>
    ///  Completes when the change log is shown, for tests.
    /// </summary>
    public Task ChangeLogLoaded => ViewModel.Loaded;
}
