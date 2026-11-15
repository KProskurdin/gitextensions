using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using GitUI.ScriptsEngine;
using Microsoft.VisualStudio.Threading;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Stages files and commits them: the new shell's version of upstream <c>FormCommit</c>, bound to
///  <see cref="CommitViewModel"/>. It works on the main window's repository state and operations, so a change made here
///  refreshes both windows. The file lists, the diff, auto-completion, spelling and the message's menus stay here.
/// </summary>
public partial class CommitWindow : Window
{
    private readonly string _repositoryPath;
    private readonly RepositoryViewModel _repository;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private readonly Func<Task> _refresh;
    private readonly Func<string, bool>? _runPlugin;
    private readonly Func<Window, string, Task>? _showSettings;

    // The file whose diff is on show, and whether it is the staged or the unstaged diff.
    private FileChange? _diffChange;
    private bool _diffStaged;

    // Upstream FormCommit's texts of the templates menu and the message box menu.
    private const string TemplateSettingsText = "_Edit commit message templates and settings...";
    private const string ConventionalCommitText = "Conven_tional Commits";
    private const string ConventionalCommitDocumentationText = "Documentation...";
    private const string WordWrapBodyText = "_Word wrap (except subject line)";
    private const string SubmoduleChangesText = "Generate a list of changes in submodules";
    private const string ShowOnlyMyMessagesText = "Show only my messages";

    // The Conventional Commits submenu as last built, and whether its types add "()" for a scope (Ctrl+Shift+T).
    private MenuItem? _conventionalCommitsItem;
    private bool _insertScopeParentheses;

    // True while the message is reformatted, so the change it makes is not formatted again.
    private bool _formatting;

    // Counts the openings of the "Commit message" menu, so a slow read does not fill a menu opened again since.
    private int _messageMenuOpenings;

    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public CommitWindow() => InitializeComponent();
#pragma warning restore CS8618

    public CommitWindow(string repositoryPath, RepositoryViewModel repository, RepositoryOperationsViewModel actions,
        IRepositoryService repositoryService, IAppPreferences preferences, Func<Task> refresh,
        ICommitMessageStore messages, Func<string, bool>? runPlugin = null,
        Func<Window, string, Task>? showSettings = null)
    {
        // Opens the browse window's settings over this one on an upstream page.
        _showSettings = showSettings;

        // Runs a plugin named by a script's plugin command, as the browse window does.
        _runPlugin = runPlugin;
        _repositoryPath = repositoryPath;
        _repository = repository;
        _actions = actions;
        _preferences = preferences;
        _refresh = refresh;
        ViewModel = new CommitViewModel(new WindowDialogHost(this), repositoryPath, repository, actions,
            repositoryService, preferences, refresh, messages, new CommitViewModelServices
            {
                ConfirmSuppressible = (confirmation, text, confirmText, caption)
                    => ConfirmWindow.AskAsync(this, preferences, confirmation, text, confirmText, caption),
                AskValidation = (question, caption)
                    => new ConfirmWindow(question, "Yes", caption, cancelText: "No").ShowDialog<bool>(this),
                RunScripts = scriptEvent
                    => new ScriptHost(this, actions, runPlugin: runPlugin).RunEventAsync(scriptEvent,
                        repositoryPath, []),
                Push = request => new PushRejectedFlow(actions, preferences).PushAsync(() => this, repositoryPath, request,
                    new PushOrigin(request.LocalBranch, request.LocalBranch, repository.TrackingRemote)),
                AskResetChanges = (hasExistingFiles, hasNewFiles)
                    => ResetChangesWindow.ShowResetDialogAsync(this, hasExistingFiles, hasNewFiles),
                CreateBranch = () => new CreateBranchWindow(repositoryPath, actions, preferences).ShowDialog<bool>(this),
                ShowError = (message, caption) => _ = new ErrorWindow(message) { Title = caption }.ShowDialog(this),
                ReadModule = ReadModuleAsync,
                StageInSuperproject = StageInSuperprojectAsync,
            });
        DataContext = ViewModel;
        InitializeComponent();

        // A flyout's items get their data context only when it opens (MVVM.md, Context menus); the options bind to it.
        foreach (Control item in ((MenuFlyout)OptionsButton.Flyout!).Items.OfType<Control>())
        {
            item.DataContext = ViewModel;
        }

        WindowPlacementTracker.Attach(this, "Xplat.CommitWindow");

        // Upstream's RestoreSplitters ("CommitDialog"): the file lists beside the diff, unstaged above staged, and the diff
        // above the message.
        SplitterTracker splitters = new(this, AppServices.Splitters, "CommitDialog");
        splitters.Add("splitMain", SplitMainGrid, rows: false, SplitterFixedPanel.Panel1);
        splitters.Add("splitRight", SplitRightGrid, rows: true, SplitterFixedPanel.Panel2);
        splitters.Add("splitLeft", SplitLeftGrid, rows: true, SplitterFixedPanel.None, splitterWidth: 6);
        StateText.Foreground = ThemeBrushes.Current.Warning;
        Opened += (_, _) => _ = ViewModel.LoadAsync();
        Closing += (_, _) => SavedDraft = ViewModel.KeepsDraft
            ? GitUI.ThreadHelper.JoinableTaskFactory.RunAsync(ViewModel.SaveDraftAsync)
            : null;
        modifyCommitMessageButton.Click += (_, _) =>
        {
            ViewModel.ModifyMessageCommand.Execute(null);
            CommitMessageBox.Focus();
        };
        ViewModel.ProgressReported += (_, count) => Dispatcher.UIThread.Post(() =>
            ViewModel.StageProgressValue =
                Math.Min(ViewModel.StageProgressMaximum - 1, ViewModel.StageProgressValue + count));
        ViewModel.PropertyChanged += OnViewModelChanged;
        CommitMessageBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                FormatMessage();
            }
        };
        AddMessageMenu();
        InitializeAutoCompletion();
        WireBottomPanel();
        WireStatusBar();

        StageAllButton.Click += (_, _) => StageAll(staged: false);
        UnstageAllButton.Click += (_, _) => StageAll(staged: true);
        WireFileLists();
        ((PopulatingMenuFlyout)CommitTemplatesFlyout).Populating += (_, _) => ShowCommitTemplates();
        ((PopulatingMenuFlyout)CommitMessageFlyout).PopulateAsync = ShowPreviousMessagesAsync;
        ((PopulatingMenuFlyout)CommitMessageFlyout).ReportError = ex => ShowError(ex.Message);
        UnstagedList.SelectionChanged += (_, _) => OnFileSelected(UnstagedList, StagedList, staged: false);
        StagedList.SelectionChanged += (_, _) => OnFileSelected(StagedList, UnstagedList, staged: true);

        // Upstream's FileViewer_TopScrollReached and FileViewer_BottomScrollReached: in the list of the shown file.
        Diff.TopScrollReached += (_, _) =>
        {
            FileListNodes.SelectNeighbourFile(_diffStaged ? StagedList : UnstagedList, next: false,
                item => item is FileChange);
            Diff.ScrollToBottom();
        };
        Diff.BottomScrollReached += (_, _) =>
        {
            FileListNodes.SelectNeighbourFile(_diffStaged ? StagedList : UnstagedList, next: true,
                item => item is FileChange);
            Diff.ScrollToTop();
        };
        Diff.LinesHotkey += (_, command) =>
        {
            // Upstream's ResetSelectedLines, after its question; here for an edited file's lines.
            if (command == DiffCommand.ResetLines)
            {
                if (ViewModel.IsIdle && Diff.SelectedRange() is { } range &&
                    _diffChange is { Kind: ChangeKind.Modified } change)
                {
                    Run(async () =>
                    {
                        if (await DiffLinePatching.ResetAsync(this, _actions, _repositoryPath, range, _diffStaged))
                        {
                            await Diff.ShowAsync(_repositoryPath, commitHash: null, change.Path, _diffStaged,
                                change.Label);
                        }
                    });
                }

                return;
            }

            bool unstage = command == DiffCommand.UnstageLines;
            if (unstage ? CanUnstageLines : CanStageLines)
            {
                Run(() => StageSelectedLinesAsync(unstage));
            }
        };
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);

        _repository.PropertyChanged += OnRepositoryChanged;
        Closed += (_, _) =>
        {
            _repository.PropertyChanged -= OnRepositoryChanged;
            ViewModel.Detach();
        };

        ShowChanges();
        ShowCommitButtons();
    }

    public CommitViewModel ViewModel { get; }

    /// <summary>
    ///  The save of the typed message when the window closed, so the next commit window can wait for it before it reads;
    ///  null while there is nothing to wait for.
    /// </summary>
    public JoinableTask? SavedDraft { get; private set; }

    /// <summary>
    ///  Upstream's <c>StartFixupCommitDialog</c>, <c>StartSquashCommitDialog</c> and <c>StartAmendCommitDialog</c>: the
    ///  window makes a <paramref name="kind"/> commit for the commit whose <paramref name="subject"/> and
    ///  <paramref name="fullMessage"/> are given, starting from upstream's message for it instead of the draft.
    /// </summary>
    public void StartSpecialCommit(CommitKind kind, string subject, string fullMessage)
        => ViewModel.StartSpecialCommit(kind, subject, fullMessage);

    private void Run(Func<Task> action) => UiActions.Run(action, ex => ShowError(ex.Message));

    // As upstream FormCommit's script options: the files selected in the list last used and the line selected in the diff.
    private Task<bool> RunScriptAsync(ScriptDefinition script)
        => new ScriptHost(this, _actions, runPlugin: _runPlugin).RunAsync(script, _repositoryPath, [],
            ScriptFileOptions.For(
                SelectedChanges(_diffStaged ? StagedList : UnstagedList).Select(change => change.Path),
                Diff.CurrentFileLine));

    private void ShowError(string message) => _ = new ErrorWindow(message).ShowDialog(this);

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (HandleAutoCompleteKey(e))
        {
            e.Handled = true;
            return;
        }

        // Upstream's FileViewer handles its hotkeys before FormCommit's.
        if (DiffView.Focused(e) is { } viewer && viewer.RunHotkey(e))
        {
            e.Handled = true;
            return;
        }

        bool command = e.KeyModifiers.HasFlag(Hotkeys.CommandModifier);
        if (e.Key == Key.Enter && command && e.Source == CommitMessageBox && CommitButton.IsEnabled)
        {
            ViewModel.CommitCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (Hotkeys.Commit.Match(e) is { } hotkey)
        {
            e.Handled = RunHotkey(hotkey);
        }
        else if (ScriptHost.MatchHotkey(e) is { } script)
        {
            e.Handled = true;
            Run(() => RunScriptAsync(script));
        }
    }

    // The commands of upstream FormCommit's hotkeys; false when the command cannot run now, so the key goes on.
    private bool RunHotkey(CommitCommand command)
    {
        switch (command)
        {
            case CommitCommand.FocusUnstagedFiles:
                return UnstagedList.Focus();
            case CommitCommand.FocusSelectedDiff:
                return Diff.FindControl<ListBox>("DiffList")?.Focus() == true;
            case CommitCommand.FocusStagedFiles:
                return StagedList.Focus();
            case CommitCommand.FocusCommitMessage:
                return CommitMessageBox.Focus();
            case CommitCommand.StageAll when StageAllButton.IsEnabled:
                StageAll(staged: false);
                return true;
            case CommitCommand.OpenWithDifftool when CanOpenDiffTool:
                Run(OpenDiffToolAsync);
                return true;
            case CommitCommand.Refresh:
                Run(_refresh);
                return true;
            case CommitCommand.SelectNext:
            case CommitCommand.SelectNextAlternativeHotkey1:
            case CommitCommand.SelectNextAlternativeHotkey2:
                return MoveSelection(1);
            case CommitCommand.SelectPrevious:
            case CommitCommand.SelectPreviousAlternativeHotkey1:
            case CommitCommand.SelectPreviousAlternativeHotkey2:
                return MoveSelection(-1);
            case CommitCommand.ConventionalCommitPrefixMessage:
                return OpenConventionalCommitMenu(insertScope: false);
            case CommitCommand.ConventionalCommitPrefixMessageWithScope:
                return OpenConventionalCommitMenu(insertScope: true);
            default:
                return false;
        }
    }

    // As upstream, next and previous move through the list whose file is shown in the diff.
    private bool MoveSelection(int step)
    {
        ListBox list = _diffStaged ? StagedList : UnstagedList;
        int index = list.SelectedIndex + step;
        if (list.ItemCount == 0 || index < 0 || index >= list.ItemCount)
        {
            return false;
        }

        list.SelectedIndex = index;
        return true;
    }

    // The lists and auto-completion follow the repository's changes; the view model follows the rest.
    private void OnRepositoryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RepositoryViewModel.Changes))
        {
            ReadChanges();
            RefreshAutoCompleteWords();
        }
    }

    // The buttons whose content is an upstream image and text follow the view model.
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CommitViewModel.CommitIcon) or nameof(CommitViewModel.CommitAndPushContent))
        {
            ShowCommitButtons();
        }
    }

    // Upstream shows one diff at a time, so selecting in one list clears the other.
    private void OnFileSelected(ListBox list, ListBox other, bool staged)
    {
        List<FileChange> selected = SelectedChanges(list);
        if (staged)
        {
            ViewModel.SelectedStaged = selected;
        }
        else
        {
            ViewModel.SelectedUnstaged = selected;
        }

        if (list.SelectedItems is not { Count: 1 } || list.SelectedItem is not FileChange change)
        {
            return;
        }

        other.SelectedItems?.Clear();
        _diffChange = change;
        _diffStaged = staged;

        // Upstream FileViewer's menu stages lines of the working directory's diff and unstages lines of the index's.
        Diff.LineAction = staged ? DiffLineAction.Unstage : DiffLineAction.Stage;
        Run(() => Diff.ShowAsync(_repositoryPath, commitHash: null, change.Path, staged, change.Label));
    }

    // As upstream, lines can be staged from an edited or untracked file and unstaged from an edited or added one.
    private bool CanStageLines => ViewModel.IsIdle && Diff.SelectedRange() is not null && !_diffStaged
                                  && _diffChange is { Kind: ChangeKind.Modified or ChangeKind.Untracked };

    private bool CanUnstageLines => ViewModel.IsIdle && Diff.SelectedRange() is not null && _diffStaged
                                    && _diffChange is { Kind: ChangeKind.Modified or ChangeKind.Added };

    private bool CanOpenDiffTool
        => ViewModel.IsIdle && _diffChange is { Kind: not (ChangeKind.Untracked or ChangeKind.Conflict) };

    private static List<FileChange> SelectedChanges(ListBox list) =>
        list.SelectedItems?.OfType<FileChange>().ToList() ?? [];

    private async Task OpenDiffToolAsync() => await OpenDiffToolAsync(tool: null);

    private async Task OpenDiffToolAsync(string? tool)
    {
        if (_diffChange is { } change)
        {
            await _actions.RunDiffToolAsync(_repositoryPath, change.Path, commit: null, _diffStaged, tool);
        }
    }

    private async Task StageSelectedLinesAsync(bool unstage)
    {
        if (_diffChange is not { } change || Diff.SelectedRange() is not { } range)
        {
            return;
        }

        if (await _actions.StageLinesAsync(_repositoryPath, range.Text, range.Start, range.Length, unstage,
                isNewFile: change.Kind == ChangeKind.Added))
        {
            // The same file's diff, read again, now without the lines that moved.
            await Diff.ShowAsync(_repositoryPath, commitHash: null, change.Path, _diffStaged, change.Label);
        }
    }

    private void StageSelected(bool staged)
        => (staged ? ViewModel.UnstageCommand : ViewModel.StageCommand).Execute(null);

    // Upstream's StageAllAccordingToFilter and UnstageAllFiles: a filtered list stages or unstages the files it shows and
    // loses its filter.
    private void StageAll(bool staged)
    {
        FileListParts parts = staged ? _staged : _unstaged;
        IReadOnlyList<FileChange>? shown = IsFiltered(parts) ? [.. parts.Shown] : null;
        parts.Filter.Text = "";
        _ = ViewModel.StageAllAsync(staged, shown);
    }

    private MenuFlyout CommitTemplatesFlyout => (MenuFlyout)CommitTemplatesButton.Flyout!;

    private MenuFlyout CommitMessageFlyout => (MenuFlyout)CommitMessageMenuButton.Flyout!;

    // Upstream's CommitMessageToolStripMenuItemDropDownOpening, read each time the menu opens; the fixed items show at once and
    // the messages are put before them when git has listed them.
    private async Task ShowPreviousMessagesAsync()
    {
        int opening = ++_messageMenuOpenings;
        CommitMessageFlyout.Items.Clear();
        MenuItem submodules = new() { Header = SubmoduleChangesText };
        submodules.Click += (_, _) => Run(InsertSubmoduleChangesAsync);
        MenuItem onlyMine = new()
        {
            Header = ShowOnlyMyMessagesText,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = _preferences.CommitDialogShowOnlyMyMessages
        };
        onlyMine.Click += (_, _) => _preferences.CommitDialogShowOnlyMyMessages = onlyMine.IsChecked;
        CommitMessageFlyout.Items.Add(new Separator());
        CommitMessageFlyout.Items.Add(submodules);
        CommitMessageFlyout.Items.Add(onlyMine);

        IReadOnlyList<string> messages = await ViewModel.PreviousMessagesAsync();
        if (opening != _messageMenuOpenings)
        {
            return;
        }

        int index = 0;
        foreach (string message in messages)
        {
            MenuItem item = new() { Header = new TextBlock { Text = PreviousCommitMessages.Label(message) } };
            item.Click += (_, _) => ReplaceMessage(message.Trim());
            CommitMessageFlyout.Items.Insert(index++, item);
        }
    }

    // Upstream's generateListOfChangesInSubmodulesChangesToolStripMenuItem_Click, for the staged changes.
    private async Task InsertSubmoduleChangesAsync()
    {
        if (await ViewModel.SubmoduleChangesAsync() is { } message)
        {
            ReplaceMessage(message);
        }
    }

    private void ReplaceMessage(string message)
    {
        if (CommitMessageBox.Text != message)
        {
            SetMessage(message, message.Length);
        }
    }

    // Upstream's commitTemplatesToolStripMenuItem_DropDownOpening, read each time it opens: the plugins' templates (e.g.
    // the GitHub plugin's assigned issues), the user's own, the Conventional Commits items and the settings. A template
    // replaces the message.
    private void ShowCommitTemplates()
    {
        CommitTemplatesFlyout.Items.Clear();
        AddGroup(CommitTemplates.Registered());
        AddGroup(CommitTemplates.FromSettings(_preferences.CommitTemplates));
        _conventionalCommitsItem = ConventionalCommitsItem();
        CommitTemplatesFlyout.Items.Add(_conventionalCommitsItem);
        CommitTemplatesFlyout.Items.Add(new Separator());
        MenuItem settings = new() { Header = TemplateSettingsText };
        settings.Click += (_, _) => Run(() => new CommitTemplateSettingsWindow(_preferences).ShowDialog(this));
        CommitTemplatesFlyout.Items.Add(settings);

        void AddGroup(IReadOnlyList<CommitTemplate> templates)
        {
            foreach (CommitTemplate template in templates)
            {
                // The name as it is: a template named after an issue may have underscores, which are not access keys here.
                MenuItem item = new() { Header = new TextBlock { Text = template.Name }, Icon = IconOf(template) };
                item.Click += (_, _) =>
                {
                    ViewModel.ApplyTemplate(template);
                    CommitMessageBox.Focus();
                };
                CommitTemplatesFlyout.Items.Add(item);
            }

            if (templates.Count > 0)
            {
                CommitTemplatesFlyout.Items.Add(new Separator());
            }
        }
    }

    // Upstream's template item image, 16 pixels as the menu's other icons.
    private static Image? IconOf(CommitTemplate template)
    {
        if (template.Icon is not { } png)
        {
            return null;
        }

        using MemoryStream stream = new(png);
        return new Image { Source = new Avalonia.Media.Imaging.Bitmap(stream), Width = 16, Height = 16 };
    }

    // Upstream's AddConventionalCommitsItems: the commit types, the footers and the documentation link.
    private MenuItem ConventionalCommitsItem()
    {
        MenuItem conventional = new() { Header = ConventionalCommitText };
        foreach (string type in ConventionalCommits.HeaderTypes)
        {
            MenuItem item = new() { Header = new TextBlock { Text = type } };
            if (type == ConventionalCommits.Feat)
            {
                item.InputGesture = Hotkeys.Commit.GestureFor(CommitCommand.ConventionalCommitPrefixMessage);
            }

            item.Click += (_, _) =>
            {
                (string message, int caret) = ConventionalCommits.ApplyType(CommitMessageBox.Text ?? "",
                    CommitMessageBox.CaretIndex, type, _insertScopeParentheses);
                SetMessage(message, caret);
            };
            conventional.Items.Add(item);
        }

        conventional.Items.Add(new Separator());
        foreach (string keyword in ConventionalCommits.FooterKeywords)
        {
            conventional.Items.Add(FooterItem(keyword, $"{keyword}: "));
        }

        conventional.Items.Add(FooterItem(ConventionalCommits.SkipCi, ConventionalCommits.SkipCi));
        conventional.Items.Add(new Separator());
        MenuItem documentation = new() { Header = ConventionalCommitDocumentationText };
        documentation.Click += (_, _) =>
            Run(() => Launcher.LaunchUriAsync(new Uri(ConventionalCommits.DocumentationUrl)));
        conventional.Items.Add(documentation);
        return conventional;

        MenuItem FooterItem(string text, string footer)
        {
            MenuItem item = new() { Header = new TextBlock { Text = text } };
            item.Click += (_, _) =>
            {
                (string message, int caret) = ConventionalCommits.AppendFooter(CommitMessageBox.Text ?? "",
                    CommitMessageBox.CaretIndex, footer);
                SetMessage(message, caret);
            };
            return item;
        }
    }

    // Upstream's OpenConventionalCommitMenu (Ctrl+T, and Ctrl+Shift+T with a scope): the menu opens at its "feat" item.
    private bool OpenConventionalCommitMenu(bool insertScope)
    {
        _insertScopeParentheses = insertScope;
        CommitTemplatesFlyout.ShowAt(CommitTemplatesButton);
        if (_conventionalCommitsItem is null)
        {
            return true;
        }

        _conventionalCommitsItem.Open();
        _conventionalCommitsItem.Items.OfType<MenuItem>()
            .FirstOrDefault(item => item.Header is TextBlock { Text: ConventionalCommits.Feat })?.Focus();
        return true;
    }

    private void SetMessage(string message, int caret)
    {
        CommitMessageBox.Text = message;
        CommitMessageBox.CaretIndex = Math.Min(caret, message.Length);
        CommitMessageBox.Focus();
    }

    // Upstream FormCommit_FormatAllText: the commit message settings shape the message while it is typed, at the caret.
    private void FormatMessage()
    {
        CommitValidationOptions options = _preferences.CommitValidation;
        if (_formatting || !(options.SecondLineMustBeEmpty || (options.AutoWrap && options.MaxLineLength > 0)))
        {
            return;
        }

        string text = CommitMessageBox.Text ?? "";
        (string formatted, int caret) = CommitMessageValidation.Format(text, CommitMessageBox.CaretIndex, options);
        if (formatted == text)
        {
            return;
        }

        _formatting = true;
        CommitMessageBox.Text = formatted;
        CommitMessageBox.CaretIndex = caret;
        _formatting = false;
    }

    // Upstream's message box menu, made each time it opens (see FillMessageMenu).
    private void AddMessageMenu()
    {
        MenuFlyout menu = new();
        menu.Opening += (_, _) => FillMessageMenu(menu);
        CommitMessageBox.ContextFlyout = menu;
        InitializeSpelling();
    }
}
