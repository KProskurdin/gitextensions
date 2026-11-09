using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Spelling;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream <c>EditNetSpell</c>'s spelling checker of the commit message: after a pause in typing the misspelled words get a
///  red wave and, with "Mark ill formed lines", the text past upstream's line lengths a yellow mark; the message box menu
///  offers the misspelled word's suggestions, "Add to dictionary", "Ignore word" and "Remove word", the dictionary to use
///  (kept for the repository) and the two settings.
/// </summary>
public partial class CommitWindow
{
    // Upstream's SpellCheckTimer.
    private static readonly TimeSpan _spellCheckDelay = TimeSpan.FromMilliseconds(250);

    private const string AddToDictionaryText = "Add to dictionary";
    private const string IgnoreWordText = "Ignore word";
    private const string RemoveWordText = "Remove word";
    private const string DictionaryText = "Dictionary";
    private const string MarkIllFormedLinesText = "Mark ill formed lines";
    private const string DeleteText = "Delete";
    private const string SelectAllText = "Select all";

    private readonly CommitSpellChecker _spellChecker = AppServices.NewSpellChecker();

    // The repository's dictionary (upstream's Detached().Dictionary); nothing is checked until it has been read.
    private string _dictionary = CommitSpellChecker.NoDictionary;
    private SpellingUnderlines? _underlines;

    // Counts the checks asked for, so only the last one shows its result.
    private int _spellCheckRequests;

    // Where the menu was asked for with the pointer; the caret otherwise, as upstream uses it.
    private int? _menuTextPosition;

    /// <summary>
    ///  The marks in the message box, for the tests; null before the box has its template.
    /// </summary>
    public SpellingUnderlines? MessageUnderlines => _underlines;

    /// <summary>
    ///  The dictionary the message is checked with.
    /// </summary>
    public string SpellingDictionary => _dictionary;

    private void InitializeSpelling()
    {
        SpellingUnderlines.Attach(CommitMessageBox, underlines => _underlines = underlines);
        CommitMessageBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                ScheduleSpellCheck();
            }
        };

        // A right click asks for the menu of the word under the pointer.
        CommitMessageBox.AddHandler(PointerPressedEvent, (_, e) =>
        {
            _menuTextPosition = e.GetCurrentPoint(CommitMessageBox).Properties.IsRightButtonPressed
                ? _underlines?.TextPositionAt(e)
                : null;
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        Opened += (_, _) => Run(LoadDictionaryAsync);
        Closed += (_, _) => _spellCheckRequests++;
    }

    private async Task LoadDictionaryAsync()
    {
        _dictionary = await SpellingDictionarySetting.LoadAsync(_repositoryPath);
        await CheckSpellingAsync(++_spellCheckRequests);
    }

    // Upstream's TextBoxTextChanged: the marks go at once; the text is checked again after the pause, unless the dictionary
    // is off or the text is shorter than four characters.
    private void ScheduleSpellCheck()
    {
        int request = ++_spellCheckRequests;
        _underlines?.Clear();
        if (_spellChecker.IsOff(_dictionary) || (CommitMessageBox.Text?.Length ?? 0) < CommitSpellChecker.MinCheckedLength)
        {
            return;
        }

        // A delayed post, as the auto-completion's: a dispatcher timer would not run without a render loop (the tests).
        _ = Task.Delay(_spellCheckDelay).ContinueWith(
            _ => Dispatcher.UIThread.Post(() =>
            {
                if (request == _spellCheckRequests)
                {
                    Run(() => CheckSpellingAsync(request));
                }
            }),
            TaskScheduler.Default);
    }

    // Upstream's CheckSpelling: the misspelled words (off the UI thread here), then the ill formed lines.
    private async Task CheckSpellingAsync(int request)
    {
        string text = CommitMessageBox.Text ?? "";
        string dictionary = _dictionary;
        IReadOnlyList<TextRange> misspelled = await Task.Run(() => _spellChecker.Misspelled(text, dictionary));
        if (request != _spellCheckRequests || text != (CommitMessageBox.Text ?? ""))
        {
            return;
        }

        bool mark = _preferences.MarkIllFormedLinesInCommitMsg;
        _underlines?.Show(misspelled, mark ? CommitSpellChecker.IllFormedLines(text) : [],
            mark && CommitSpellChecker.MarksEmptySubject(text));
    }

    private void CheckSpellingNow() => Run(() => CheckSpellingAsync(++_spellCheckRequests));

    // Upstream's SpellCheckContextMenuOpening and FormCommit's Message_ContextMenuPopulating: the misspelled word's items, the
    // editing items, the dictionaries, the two settings, and "Word wrap" before the first separator.
    private void FillMessageMenu(MenuFlyout menu)
    {
        string text = CommitMessageBox.Text ?? "";
        int position = Math.Clamp(_menuTextPosition ?? CommitMessageBox.CaretIndex, 0, text.Length);
        _menuTextPosition = null;
        List<Control> items = [];

        if (_spellChecker.SuggestionsAt(text, position, _dictionary) is { } word)
        {
            foreach (string suggestion in word.Suggestions)
            {
                items.Add(Item(suggestion, () => ReplaceWord(word.Range, suggestion), bold: true));
            }

            items.Add(Item(AddToDictionaryText, () =>
            {
                _spellChecker.AddToDictionary(word.Word, _dictionary);
                CheckSpellingNow();
            }));
            items.Add(Item(IgnoreWordText, () =>
            {
                _spellChecker.Ignore(word.Word);
                CheckSpellingNow();
            }));
            items.Add(Item(RemoveWordText, () => SetMessage(CommitSpellChecker.RemoveWord(text, word.Range), word.Range.Start)));
            if (word.Suggestions.Count > 0)
            {
                items.Add(new Separator());
            }
        }

        MenuItem cut = Item("Cut", () => CommitMessageBox.Cut());
        cut.InputGesture = new KeyGesture(Key.X, Hotkeys.CommandModifier);
        MenuItem copy = Item("Copy", () => CommitMessageBox.Copy());
        copy.InputGesture = new KeyGesture(Key.C, Hotkeys.CommandModifier);
        MenuItem paste = Item("Paste", () => CommitMessageBox.Paste());
        paste.InputGesture = new KeyGesture(Key.V, Hotkeys.CommandModifier);
        items.AddRange([cut, copy, paste, Item(DeleteText, DeleteSelection), Item(SelectAllText, () => CommitMessageBox.SelectAll())]);
        items.Add(new Separator());
        items.Add(DictionaryMenuItem());
        items.Add(new Separator());

        MenuItem markLines = new()
        {
            Header = MarkIllFormedLinesText,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = _preferences.MarkIllFormedLinesInCommitMsg,
        };
        markLines.Click += (_, _) =>
        {
            _preferences.MarkIllFormedLinesInCommitMsg = !_preferences.MarkIllFormedLinesInCommitMsg;
            CheckSpellingNow();
        };
        items.Add(markLines);
        items.Add(AutoCompletionMenuItem());

        MenuItem wordWrap = Item(WordWrapBodyText, () => CommitMessageBox.Text =
            CommitMessageValidation.WrapBody(CommitMessageBox.Text ?? "", _preferences.CommitValidation.MaxLineLength));
        int separator = items.FindIndex(item => item is Separator);
        items.Insert(separator < 0 ? 0 : separator, wordWrap);

        menu.Items.Clear();
        foreach (Control item in items)
        {
            menu.Items.Add(item);
        }
    }

    // Upstream's AddDictionaries: "None" and the dictionaries next to the app, the one in use checked; a choice is kept for
    // the repository (DicToolStripMenuItemClick).
    private MenuItem DictionaryMenuItem()
    {
        MenuItem menu = new() { Header = DictionaryText };
        foreach (string dictionary in (string[])[CommitSpellChecker.NoDictionary, .. CommitSpellChecker.Dictionaries(AppServices.DictionaryFolder)])
        {
            MenuItem item = new()
            {
                Header = new TextBlock { Text = dictionary },
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = string.Equals(dictionary, _dictionary, StringComparison.OrdinalIgnoreCase),
            };
            item.Click += (_, _) => Run(async () =>
            {
                _dictionary = dictionary;
                await SpellingDictionarySetting.SaveLocalAsync(_repositoryPath, dictionary);
                await CheckSpellingAsync(++_spellCheckRequests);
            });
            menu.Items.Add(item);
        }

        return menu;
    }

    // Upstream's SpellingReplacedWord: the word replaced, the caret after it.
    private void ReplaceWord(TextRange range, string replacement)
    {
        string text = CommitMessageBox.Text ?? "";
        SetMessage(text.Remove(range.Start, range.Length).Insert(range.Start, replacement), range.Start + replacement.Length);
    }

    // Upstream's DeleteMenuItemClick: the selected text goes.
    private void DeleteSelection()
    {
        string text = CommitMessageBox.Text ?? "";
        int start = Math.Min(CommitMessageBox.SelectionStart, CommitMessageBox.SelectionEnd);
        int length = Math.Abs(CommitMessageBox.SelectionEnd - CommitMessageBox.SelectionStart);
        if (length > 0)
        {
            SetMessage(text.Remove(start, length), start);
        }
    }

    private static MenuItem Item(string text, Action click, bool bold = false)
    {
        MenuItem item = new()
        {
            Header = bold ? new TextBlock { Text = text, FontWeight = FontWeight.Bold } : text,
        };
        item.Click += (_, _) => click();
        return item;
    }
}
