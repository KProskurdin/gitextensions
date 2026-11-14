using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The new shell's version of upstream <c>FormBisect</c>, bound to <see cref="BisectViewModel"/>: Start runs while the
///  window stays open, so the first mark can follow at once, as upstream; the other buttons close the window with their
///  <see cref="BisectStep"/>, which the browse window runs.
/// </summary>
public partial class BisectWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
    public BisectWindow() => InitializeComponent();

    /// <param name="isBisecting">Whether a bisect is in progress.</param>
    /// <param name="start">Starts the bisect; true when it started.</param>
    public BisectWindow(bool isBisecting, Func<Task<bool>> start)
    {
        DataContext = new BisectViewModel(new WindowDialogHost(this), isBisecting, start);
        InitializeComponent();
    }
}
