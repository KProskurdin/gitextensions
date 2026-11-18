using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Spelling;

namespace GitExtensions.Xplat.Core.Repository;

// Upstream FormCommit's menus filled as they open, as entries the view shows (MVVM.md): "Commit message" (the earlier
// messages), "Commit templates" (the templates, Conventional Commits and the settings) and the settings of the message
// box's menu (word wrap, the dictionary, "Mark ill formed lines", auto-completion). The message box's editing items and
// the misspelled word's items act on the box's selection and stay the view's.
public sealed partial class CommitViewModel
{
    // Upstream FormCommit and EditNetSpell texts.
    public const string SubmoduleChangesText = "Generate a list of changes in submodules";
    public const string ShowOnlyMyMessagesText = "Show only my messages";
    public const string TemplateSettingsText = "_Edit commit message templates and settings...";
    public const string ConventionalCommitText = "Conven_tional Commits";
    public const string ConventionalCommitDocumentationText = "Documentation...";
    public const string WordWrapBodyText = "_Word wrap (except subject line)";
    public const string DictionaryText = "Dictionary";
    public const string MarkIllFormedLinesText = "Mark ill formed lines";
    public const string AutoCompletionText = "Provide auto completion";

    /// <summary>
    ///  The name of the Conventional Commits item, which the hotkeys open.
    /// </summary>
    public const string ConventionalCommitsName = "ConventionalCommitsMenuItem";

    /// <summary>
    ///  Raised when the view model replaced the message: the window puts the caret there and focuses the message.
    /// </summary>
    public event EventHandler<int>? CaretRequested;

    /// <summary>
    ///  The caret in the message box, which the view passes; Conventional Commits' types and footers go there.
    /// </summary>
    public int MessageCaret { get; set; }

    /// <summary>
    ///  Upstream's <c>OpenConventionalCommitMenu</c> with a scope (Ctrl+Shift+T): the types add "()" after them.
    /// </summary>
    public bool InsertScopeParentheses { get; set; }

    /// <summary>
    ///  The repository's spelling dictionary (upstream's <c>Detached().Dictionary</c>); nothing is checked until it has been
    ///  read.
    /// </summary>
    [ObservableProperty]
    public partial string SpellingDictionary { get; private set; } = CommitSpellChecker.NoDictionary;

    /// <summary>
    ///  Upstream's <c>MarkIllFormedLinesInCommitMsg</c>, which the message box's menu turns on and off.
    /// </summary>
    public bool MarkIllFormedLines => _preferences.MarkIllFormedLinesInCommitMsg;

    /// <summary>
    ///  Upstream's <c>ProvideAutocompletion</c>, which the message box's menu turns on and off.
    /// </summary>
    public bool ProvideAutoCompletion => _preferences.ProvideAutocompletion;

    /// <summary>
    ///  The "Commit message" menu's fixed items, shown at once as it opens.
    /// </summary>
    public IReadOnlyList<MenuEntry> MessageMenuEntries() =>
    [
        MenuEntry.Separator(),
        new(Host.Translate(SubmoduleChangesText), InsertSubmoduleChangesCommand),
        new(Host.Translate(ShowOnlyMyMessagesText), ToggleShowOnlyMyMessagesCommand)
        {
            IsChecked = _preferences.CommitDialogShowOnlyMyMessages,
        },
    ];

    /// <summary>
    ///  Upstream's CommitMessageToolStripMenuItemDropDownOpening: the earlier messages, read each time the menu opens, which
    ///  go before the fixed items.
    /// </summary>
    public async Task<IReadOnlyList<MenuEntry>> PreviousMessageEntriesAsync()
        => [.. (await PreviousMessagesAsync()).Select(message =>
            new MenuEntry(PreviousCommitMessages.Label(message), UseMessageCommand, message.Trim()))];

    /// <summary>
    ///  Upstream's commitTemplatesToolStripMenuItem_DropDownOpening, read each time it opens: the plugins' templates (e.g.
    ///  the GitHub plugin's assigned issues), the user's own, the Conventional Commits items and the settings.
    /// </summary>
    public IReadOnlyList<MenuEntry> TemplateMenuEntries()
    {
        List<MenuEntry> entries = [];
        AddGroup(CommitTemplates.Registered());
        AddGroup(CommitTemplates.FromSettings(_preferences.CommitTemplates));
        entries.Add(ConventionalCommitsEntry());
        entries.Add(MenuEntry.Separator());
        entries.Add(new MenuEntry(Host.Translate(TemplateSettingsText), EditTemplatesCommand) { HasAccessKey = true });
        return entries;

        void AddGroup(IReadOnlyList<CommitTemplate> templates)
        {
            // The name as it is: a template named after an issue may have underscores, which are not access keys here.
            entries.AddRange(templates.Select(template =>
                new MenuEntry(template.Name, UseTemplateCommand, template) { ImageData = template.Icon }));
            if (templates.Count > 0)
            {
                entries.Add(MenuEntry.Separator());
            }
        }
    }

    /// <summary>
    ///  FormCommit's "Word wrap" item of the message box's menu.
    /// </summary>
    public MenuEntry WordWrapEntry() => new(Host.Translate(WordWrapBodyText), WrapMessageBodyCommand) { HasAccessKey = true };

    /// <summary>
    ///  Upstream's AddDictionaries: <paramref name="dictionaries"/> (with "None"), the one in use checked.
    /// </summary>
    public MenuEntry DictionaryEntry(IEnumerable<string> dictionaries) => new(Host.Translate(DictionaryText))
    {
        Children =
        [
            .. dictionaries.Select(dictionary => new MenuEntry(dictionary, ChooseDictionaryCommand, dictionary)
            {
                IsChecked = string.Equals(dictionary, SpellingDictionary, StringComparison.OrdinalIgnoreCase),
            })
        ],
    };

    /// <summary>
    ///  The message box menu's two settings: "Mark ill formed lines" and "Provide auto completion".
    /// </summary>
    public IReadOnlyList<MenuEntry> MessageBoxSettingEntries() =>
    [
        new(Host.Translate(MarkIllFormedLinesText), ToggleMarkIllFormedLinesCommand) { IsChecked = MarkIllFormedLines },
        new(Host.Translate(AutoCompletionText), ToggleAutoCompletionCommand) { IsChecked = ProvideAutoCompletion },
    ];

    // Upstream's Conventional Commits items: the commit types, the footers and the documentation link.
    private MenuEntry ConventionalCommitsEntry()
    {
        List<MenuEntry> children =
        [
            .. ConventionalCommits.HeaderTypes.Select(type => new MenuEntry(type, ApplyConventionalTypeCommand, type)),
            MenuEntry.Separator(),
            .. ConventionalCommits.FooterKeywords.Select(keyword =>
                new MenuEntry(keyword, AppendFooterCommand, $"{keyword}: ")),
            new(ConventionalCommits.SkipCi, AppendFooterCommand, ConventionalCommits.SkipCi),
            MenuEntry.Separator(),
            new(Host.Translate(ConventionalCommitDocumentationText), OpenConventionalCommitsDocumentationCommand),
        ];
        return new MenuEntry(Host.Translate(ConventionalCommitText))
        {
            HasAccessKey = true,
            Name = ConventionalCommitsName,
            Children = children,
        };
    }

    private Task LoadDictionaryAsync()
        => _services.LoadDictionary is { } load
            ? GuardAsync(async () => SpellingDictionary = await load())
            : Task.CompletedTask;

    // An earlier message replaces the message, the caret at its end.
    [RelayCommand]
    private void UseMessage(string message)
    {
        if (Message != message)
        {
            SetMessage(message, message.Length);
        }
    }

    // Upstream's generateListOfChangesInSubmodulesChangesToolStripMenuItem_Click.
    [RelayCommand]
    private Task InsertSubmoduleChangesAsync() => GuardAsync(async () =>
    {
        if (await SubmoduleChangesAsync() is { } message)
        {
            UseMessage(message);
        }
    });

    [RelayCommand]
    private void ToggleShowOnlyMyMessages()
        => SavePreference(() => _preferences.CommitDialogShowOnlyMyMessages = !_preferences.CommitDialogShowOnlyMyMessages);

    // A template replaces the message, and the message is next.
    [RelayCommand]
    private void UseTemplate(CommitTemplate template)
    {
        ApplyTemplate(template);
        MessageFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private Task EditTemplatesAsync() => _services.EditTemplates is { } edit ? GuardAsync(edit) : Task.CompletedTask;

    // Upstream's PrefixOrReplaceKeyword at the caret.
    [RelayCommand]
    private void ApplyConventionalType(string type)
    {
        (string message, int caret) = ConventionalCommits.ApplyType(Message, MessageCaret, type, InsertScopeParentheses);
        SetMessage(message, caret);
    }

    [RelayCommand]
    private void AppendFooter(string footer)
    {
        (string message, int caret) = ConventionalCommits.AppendFooter(Message, MessageCaret, footer);
        SetMessage(message, caret);
    }

    [RelayCommand]
    private Task OpenConventionalCommitsDocumentationAsync()
        => _services.OpenUrl is { } open
            ? GuardAsync(() => open(new Uri(ConventionalCommits.DocumentationUrl)))
            : Task.CompletedTask;

    // FormCommit's wordWrapToolStripMenuItem: the body wrapped at the commit message settings' line length.
    [RelayCommand]
    private void WrapMessageBody() => Message = CommitMessageValidation.WrapBody(Message,
        _preferences.CommitValidation.MaxLineLength);

    // Upstream's DicToolStripMenuItemClick: the choice is the repository's local setting.
    [RelayCommand]
    private Task ChooseDictionaryAsync(string dictionary) => GuardAsync(async () =>
    {
        SpellingDictionary = dictionary;
        if (_services.SaveDictionary is { } save)
        {
            await save(dictionary);
        }
    });

    [RelayCommand]
    private void ToggleMarkIllFormedLines()
    {
        _preferences.MarkIllFormedLinesInCommitMsg = !_preferences.MarkIllFormedLinesInCommitMsg;
        OnPropertyChanged(nameof(MarkIllFormedLines));
    }

    [RelayCommand]
    private void ToggleAutoCompletion()
    {
        _preferences.ProvideAutocompletion = !_preferences.ProvideAutocompletion;
        OnPropertyChanged(nameof(ProvideAutoCompletion));
    }

    private void SetMessage(string message, int caret)
    {
        Message = message;
        CaretRequested?.Invoke(this, Math.Min(caret, message.Length));
    }
}
