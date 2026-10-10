using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's quick search of the revision grid: typing into the grid selects the next commit that matches, Alt+Down and
///  Alt+Up find the next and previous match, and the search ends after upstream's timeout, with Escape or another key.
/// </summary>
public partial class MainWindow
{
    private readonly QuickSearch _quickSearch = new();
    private DispatcherTimer? _quickSearchTimer;

    private void WireQuickSearch()
    {
        CommitList.AddHandler(TextInputEvent, (_, e) =>
        {
            if (e.Text is { Length: > 0 } text && !text.Any(char.IsControl))
            {
                QuickSearchTo(_quickSearch.Type(text, ShownRows(), CommitList.SelectedIndex));
                e.Handled = true;
            }
        });
        CommitList.AddHandler(KeyDownEvent, (_, e) => Run(() => OnQuickSearchKeyAsync(e)),
            Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private async Task OnQuickSearchKeyAsync(KeyEventArgs e)
    {
        if (!_quickSearch.IsActive && e.Key != Key.V)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Back when _quickSearch.IsActive:
                QuickSearchTo(_quickSearch.Backspace(ShownRows(), CommitList.SelectedIndex));
                e.Handled = true;
                break;
            case Key.V when e.KeyModifiers == Hotkeys.CommandModifier && _quickSearch.IsActive:
                e.Handled = true;
                if (await (Clipboard?.TryGetTextAsync() ?? Task.FromResult<string?>(null)) is { Length: > 0 } text)
                {
                    QuickSearchTo(_quickSearch.Type(text, ShownRows(), CommitList.SelectedIndex));
                }

                break;
            case Key.Escape or Key.Enter or Key.Tab:
                // Upstream: Escape, and a control character typed into the grid, end the search; the key keeps its meaning.
                EndQuickSearch();
                break;
        }
    }

    // Upstream's NextResult (Alt+Down, Alt+Up): the last search again from the row after or before the selected one.
    private bool QuickSearchNext(bool down)
    {
        QuickSearchTo(_quickSearch.Next(ShownRows(), CommitList.SelectedIndex, down));
        return true;
    }

    private IReadOnlyList<CommitRow> ShownRows() => [.. _commits.VisibleRows.Select(item => item.Row)];

    private void QuickSearchTo(int? index)
    {
        if (index is { } row && CommitList.SelectedIndex != row)
        {
            SelectGridRow(row);
        }

        QuickSearchText.Text = _quickSearch.Text;
        QuickSearchText.Foreground = _quickSearch.Found ? null : ThemeBrushes.Current.Warning;
        QuickSearchPanel.IsVisible = true;
        _quickSearchTimer ??= new DispatcherTimer();
        _quickSearchTimer.Stop();
        _quickSearchTimer.Interval =
            TimeSpan.FromMilliseconds(Math.Max(1, _preferences.RevisionGridQuickSearchTimeout));
        _quickSearchTimer.Tick -= OnQuickSearchTimeout;
        _quickSearchTimer.Tick += OnQuickSearchTimeout;
        _quickSearchTimer.Start();
    }

    private void OnQuickSearchTimeout(object? sender, EventArgs e) => EndQuickSearch();

    private void EndQuickSearch()
    {
        _quickSearchTimer?.Stop();
        _quickSearch.End();
        QuickSearchPanel.IsVisible = false;
    }
}
