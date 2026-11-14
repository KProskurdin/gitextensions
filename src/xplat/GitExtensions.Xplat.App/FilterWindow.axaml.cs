using Avalonia.Controls;
using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormRevisionFilter</c>, bound to <see cref="FilterViewModel"/>: each filter turned on by its check box,
///  and the branch and history options, with upstream's <c>UpdateFilters</c> rules. Closes with the new filter, or null
///  when cancelled.
/// </summary>
public partial class FilterWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
    public FilterWindow() => InitializeComponent();

    /// <param name="defaultLimit">Upstream's <c>CommitsLimitDefault</c> (<c>MaxRevisionGraphCommits</c>), the limit shown
    ///  while none is set.</param>
    public FilterWindow(RevisionFilter current, int defaultLimit = 0)
    {
        DataContext = new FilterViewModel(new WindowDialogHost(this), current, defaultLimit);
        InitializeComponent();
        InitialFocus.Attach(this);
    }
}
