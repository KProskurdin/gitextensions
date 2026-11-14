using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>SearchWindow</c> with its <c>SearchControl</c>, for "Find file", bound to <see cref="SearchViewModel"/>:
///  while the user types, the first matches drop down below the text box; Up and Down move through them, Enter (or a
///  double-click) takes the selected one and Escape closes without one. Upstream has no translation for it (a generic
///  form).
/// </summary>
public partial class SearchWindow : Window
{
    public SearchWindow()
        : this(_ => [])
    {
    }

    /// <param name="getCandidates">The matches for a text; called off the UI thread, as upstream's AsyncLoader does.</param>
    public SearchWindow(Func<string, IEnumerable<string>> getCandidates)
    {
        ViewModel = new SearchViewModel(new WindowDialogHost(this), getCandidates);
        DataContext = ViewModel;
        InitializeComponent();

        // View concerns: the text box's keys (it would handle Up, Down and Enter itself), and the double-click.
        txtSearchBox.AddHandler(KeyDownEvent, OnSearchBoxKeyDown, RoutingStrategies.Tunnel);
        listBoxSearchResult.DoubleTapped += (_, _) => ViewModel.TakeCommand.Execute(null);
        InitialFocus.Attach(this, () => txtSearchBox);
    }

    public SearchViewModel ViewModel { get; }

    /// <summary>
    ///  The match the user took; null when the window was closed without one.
    /// </summary>
    public string? SelectedItem => ViewModel.SelectedItem;

    /// <summary>
    ///  The search of the latest text: done once its matches show.
    /// </summary>
    public Task Searched => ViewModel.Searched;

    /// <summary>
    ///  Shows the window over <paramref name="owner"/> and returns the match the user took.
    /// </summary>
    public async Task<string?> ShowAsync(Window owner)
    {
        await ShowDialog(owner);
        return SelectedItem;
    }

    // Upstream's txtSearchBox_KeyDown and txtSearchBox_KeyUp.
    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down when ViewModel.Candidates.Count > 1:
                e.Handled = true;
                ViewModel.MoveDownCommand.Execute(null);
                break;
            case Key.Up when ViewModel.Candidates.Count > 1:
                e.Handled = true;
                ViewModel.MoveUpCommand.Execute(null);
                break;
            case Key.Enter:
                e.Handled = true;
                ViewModel.TakeCommand.Execute(null);
                break;
            case Key.Escape:
                e.Handled = true;
                ViewModel.CancelCommand.Execute(null);
                break;
        }
    }
}
