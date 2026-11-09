using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using GitExtensions.Xplat.Core.Operations;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's output history in the browse window (<c>OutputHistoryControl</c>, <c>OutputHistoryTabController</c>,
///  <c>OutputHistoryPanelController</c>): the history as read-only text in the fixed-width font, scrolled to its end, with
///  Copy and Clear; a tab of the lower pane, or a panel below the left panel that the hotkey shows and hides.
/// </summary>
public partial class MainWindow
{
    private const string CopyText = "Copy";
    private const string ClearText = "Clear";

    private TextBox? _outputHistoryText;

    /// <summary>
    ///  The output history as shown, for the tests; null while the history is off.
    /// </summary>
    public string? OutputHistoryText => _outputHistoryText?.Text;

    private void WireOutputHistory()
    {
        OutputHistory history = AppServices.OutputHistory;
        _actions.History = history;
        if (!history.Enabled)
        {
            return;
        }

        TextBox text = new()
        {
            Name = "OutputHistoryText",
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            BorderThickness = new Avalonia.Thickness(0),
        };
        text.Bind(TextBox.FontFamilyProperty, this.GetResourceObservable("MonospaceFontFamily"));
        text.Bind(TextBox.FontSizeProperty, this.GetResourceObservable("MonospaceFontSize"));
        MenuItem copy = new() { Header = CopyText };
        copy.Click += (_, _) => Run(() => CopyTextAsync(text.SelectedText is { Length: > 0 } selected ? selected : text.Text));
        MenuItem clear = new() { Header = ClearText };
        clear.Click += (_, _) => history.Clear();
        text.ContextMenu = new ContextMenu { Items = { copy, clear } };
        _outputHistoryText = text;

        if (_preferences.ShowOutputHistoryAsTab)
        {
            OutputHistoryTab.Content = text;
            OutputHistoryTab.IsVisible = true;
        }
        else
        {
            OutputHistoryPanel.Child = text;
            OutputHistoryPanel.IsVisible = _preferences.OutputHistoryPanelVisible;
        }

        void OnChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() => ShowOutputHistory(history));
        history.HistoryChanged += OnChanged;
        Closed += (_, _) => history.HistoryChanged -= OnChanged;
        ShowOutputHistory(history);
    }

    // Upstream's Update: the whole history, the caret and view at its end.
    private void ShowOutputHistory(OutputHistory history)
    {
        if (_outputHistoryText is not { } text)
        {
            return;
        }

        text.Text = history.History;
        text.CaretIndex = text.Text.Length;
    }

    // Upstream's FocusAndToggleIfPanel: the tab is selected and focused; the panel is shown or hidden, and focused when shown.
    private bool FocusOutputHistoryAndToggleIfPanel()
    {
        if (_outputHistoryText is not { } text)
        {
            return false;
        }

        if (OutputHistoryTab.IsVisible)
        {
            DetailTabs.SelectedItem = OutputHistoryTab;
            text.Focus();
            return true;
        }

        bool show = !_preferences.OutputHistoryPanelVisible;
        _preferences.OutputHistoryPanelVisible = show;
        OutputHistoryPanel.IsVisible = show;
        if (show)
        {
            if (!LeftPanel.IsVisible)
            {
                ToggleLeftPanel();
            }

            text.Focus();
        }

        return true;
    }
}
