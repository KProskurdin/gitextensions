using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitExtensions.Xplat.Core.Repository;
using GitUI.AutoCompletion;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream <c>EditNetSpell</c>'s auto-completion of the commit message (setting <c>ProvideAutocompletion</c>): typing a word
///  lists the words of the changed files that complete it, Ctrl+Space asks for the list (taking the only match at once),
///  Tab or Enter takes the selected word and Escape closes the list.
/// </summary>
public partial class CommitWindow
{
    // Upstream's AutoCompleteTimer: the list follows typing after this pause.
    private static readonly TimeSpan _autoCompleteDelay = TimeSpan.FromMilliseconds(200);

    private const string AutoCompletionText = "Provide auto completion";

    // Counts the requests to follow the typing, so only the last one after the pause shows the list.
    private int _autoCompleteRequests;
    private CancellationTokenSource _autoCompleteCancellation = new();
    private Task<IReadOnlyList<AutoCompleteWord>>? _autoCompleteWords;
    private IReadOnlyList<AutoCompleteWord> _autoCompleteMatches = [];

    // Upstream's _autoCompleteWasUserActivated: after Ctrl+Space the list also follows a word of one letter.
    private bool _autoCompleteWasUserActivated;

    /// <summary>
    ///  The words the list currently offers; empty when it is closed.
    /// </summary>
    public IReadOnlyList<string> AutoCompleteOffers
        => AutoCompletePopup.IsOpen ? [.. _autoCompleteMatches.Select(word => word.Word)] : [];

    private void InitializeAutoCompletion()
    {
        AutoCompletePopup.PlacementTarget = CommitMessageBox;

        // The message box marks the input handled as it inserts it.
        CommitMessageBox.AddHandler(TextInputEvent, OnMessageTextInput, RoutingStrategies.Bubble,
            handledEventsToo: true);
        CommitMessageBox.LostFocus += (_, _) => CloseAutoComplete();
        AutoCompleteList.Tapped += (_, _) => AcceptAutoComplete();
        Opened += (_, _) => RefreshAutoCompleteWords();
        Closed += (_, _) =>
        {
            _autoCompleteRequests++;
            _autoCompleteCancellation.Cancel();
        };
    }

    // Upstream's RefreshAutoCompleteWords, when the window opens and the changes are read again.
    private void RefreshAutoCompleteWords()
    {
        _autoCompleteCancellation.Cancel();
        _autoCompleteCancellation = new CancellationTokenSource();
        _autoCompleteWords = _preferences.ProvideAutocompletion
            ? CommitMessageAutoCompletion.LoadWordsAsync(_repositoryPath, _autoCompleteCancellation.Token)
            : null;
    }

    // Upstream's message box menu item.
    private MenuItem AutoCompletionMenuItem()
    {
        MenuItem item = new()
        {
            Header = AutoCompletionText,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = _preferences.ProvideAutocompletion
        };
        item.Click += (_, _) =>
        {
            _preferences.ProvideAutocompletion = item.IsChecked;
            CloseAutoComplete();
            RefreshAutoCompleteWords();
        };
        return item;
    }

    // Upstream's TextBox_KeyPress: a separator closes the list, other typing shows it after the pause.
    private void OnMessageTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text) || e.Text.Any(CommitMessageAutoCompletion.IsSeparator))
        {
            CloseAutoComplete();
            return;
        }

        ScheduleAutoComplete();
    }

    // Upstream's AutoCompleteTimer, restarted by each key: the list follows the typing once it pauses.
    private void ScheduleAutoComplete()
    {
        int request = ++_autoCompleteRequests;
        _ = Task.Delay(_autoCompleteDelay).ContinueWith(
            _ => Dispatcher.UIThread.Post(() =>
            {
                if (request == _autoCompleteRequests)
                {
                    UpdateOrShowAutoComplete(calledByUser: false);
                }
            }),
            TaskScheduler.Default);
    }

    // Before the window's own keys (Escape closes the window); true when the key was the list's.
    private bool HandleAutoCompleteKey(KeyEventArgs e)
    {
        if (e.Source != CommitMessageBox)
        {
            return false;
        }

        if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.Control && _preferences.ProvideAutocompletion)
        {
            UpdateOrShowAutoComplete(calledByUser: true);
            return true;
        }

        if (e.Key == Key.Back)
        {
            // As upstream: deleting a separator, or at the start, closes the list; otherwise it follows the shorter word.
            string text = CommitMessageBox.Text ?? "";
            int caret = CommitMessageBox.CaretIndex;
            if (caret == 0 || CommitMessageAutoCompletion.IsSeparator(text[Math.Min(caret, text.Length) - 1]))
            {
                CloseAutoComplete();
            }
            else if (AutoCompletePopup.IsOpen)
            {
                ScheduleAutoComplete();
            }

            return false;
        }

        if (!AutoCompletePopup.IsOpen || e.KeyModifiers != KeyModifiers.None)
        {
            return false;
        }

        switch (e.Key)
        {
            case Key.Tab or Key.Enter:
                AcceptAutoComplete();
                return true;
            case Key.Escape:
                CloseAutoComplete();
                return true;
            case Key.Down:
                AutoCompleteList.SelectedIndex = Math.Min(AutoCompleteList.SelectedIndex + 1, AutoCompleteList.ItemCount - 1);
                AutoCompleteList.ScrollIntoView(AutoCompleteList.SelectedIndex);
                return true;
            case Key.Up:
                AutoCompleteList.SelectedIndex = Math.Max(AutoCompleteList.SelectedIndex - 1, 0);
                AutoCompleteList.ScrollIntoView(AutoCompleteList.SelectedIndex);
                return true;
            default:
                return false;
        }
    }

    // Upstream's UpdateOrShowAutoComplete.
    private void UpdateOrShowAutoComplete(bool calledByUser)
    {
        if (!_preferences.ProvideAutocompletion || _autoCompleteWords is null)
        {
            return;
        }

        if (!_autoCompleteWords.IsCompletedSuccessfully)
        {
            // Upstream says the words are still being read; the list follows the next key instead.
            if (calledByUser)
            {
                _autoCompleteWords.ContinueWith(_ => Dispatcher.UIThread.Post(() => UpdateOrShowAutoComplete(true)),
                    TaskScheduler.Default);
            }

            return;
        }

        string text = CommitMessageBox.Text ?? "";
        int caret = Math.Min(CommitMessageBox.CaretIndex, text.Length);
        string word = CommitMessageAutoCompletion.WordAtCursor(text, caret);
        if (word.Length <= 1 && !calledByUser && !_autoCompleteWasUserActivated)
        {
            CloseAutoComplete();
            return;
        }

        IReadOnlyList<AutoCompleteWord> matches = CommitMessageAutoCompletion.Matches(_autoCompleteWords.Result, word);
        if (matches.Count == 0)
        {
            CloseAutoComplete();
            return;
        }

        if (matches.Count == 1 && calledByUser)
        {
            AcceptAutoComplete(matches[0]);
            return;
        }

        _autoCompleteWasUserActivated |= calledByUser;
        _autoCompleteMatches = matches;
        AutoCompleteList.ItemsSource = matches.Select(match => match.Word).ToList();
        AutoCompleteList.SelectedIndex = 0;
        AutoCompletePopup.PlacementRect = CaretRect();
        AutoCompletePopup.IsOpen = true;
    }

    private void AcceptAutoComplete(AutoCompleteWord? word = null)
    {
        word ??= AutoCompleteList.SelectedIndex is >= 0 and var index && index < _autoCompleteMatches.Count
            ? _autoCompleteMatches[index]
            : null;
        if (word is not null)
        {
            string text = CommitMessageBox.Text ?? "";
            (string completed, int caret) = CommitMessageAutoCompletion.Accept(text,
                Math.Min(CommitMessageBox.CaretIndex, text.Length), word.Word);
            SetMessage(completed, caret);
        }

        CloseAutoComplete();
    }

    private void CloseAutoComplete()
    {
        _autoCompleteRequests++;
        AutoCompletePopup.IsOpen = false;
        _autoCompleteMatches = [];
        _autoCompleteWasUserActivated = false;
    }

    // The caret's line in the message box, for the list to open below it.
    private Rect CaretRect()
    {
        if (CommitMessageBox.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault() is { } presenter
            && presenter.TranslatePoint(default, CommitMessageBox) is { } origin)
        {
            Rect caret = presenter.TextLayout.HitTestTextPosition(CommitMessageBox.CaretIndex);
            return caret.Translate(origin);
        }

        return new Rect(0, 0, 1, CommitMessageBox.Bounds.Height);
    }
}
