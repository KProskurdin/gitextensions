using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitCommands.Settings;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  Upstream <c>FileViewer</c> without a control (PLAN.md principle 6): what the viewer shows (a diff, a file's text, git-grep's
///  matches, a picture or the link to a large file), its toolbar and menu state, the options and the commands. The view
///  draws the lines in the theme's colors, finds in them and keeps the selection; it tells this the selection, the pointer
///  and its width. Holds no UI types: a picture is the view's decoded image, kept as an object.
/// </summary>
public sealed partial class DiffViewModel : ObservableObject
{
    // Upstream's text of the menu item and of the setting.
    public const string ContinuousScrollText = "Enable automatic continuous scroll (without ALT button)";

    /// <summary>
    ///  Shown instead of a file that is not text.
    /// </summary>
    public const string BinaryFileNotShown = "Binary file, not shown.";

    // Upstream's DiffViewerSettingsPage.GetPageReference(), as the settings window names its pages.
    public const string DiffViewerSettingsPage = "DiffViewerSettingsPage";

    // Git's line for a new binary file; upstream shows a new file itself, not a diff.
    private const string NewBinaryFilePrefix = "Binary files /dev/null and ";
    private const double BytesPerMb = 1024d * 1024;

    // Upstream's _difftasticCmdCache: whether git knows the tool, read once per repository.
    private static readonly ConcurrentDictionary<string, bool> _difftasticEnabled = [];

    // The properties that follow the menu's state and the options.
    private static readonly string[] _menuProperties =
    [
        nameof(HasSelectedLines), nameof(IsStageLinesVisible), nameof(IsUnstageLinesVisible), nameof(IsResetLinesVisible),
        nameof(IsCopyPatchVisible), nameof(AreCopyVersionsVisible), nameof(IsDiffAppearanceVisible), nameof(IsPatchAppearance),
        nameof(IsGitWordDiffAppearance), nameof(IsDifftasticAppearance), nameof(IsPartialTextView),
        nameof(IsContinuousScrollVisible), nameof(AutomaticContinuousScroll),
    ];

    private static readonly string[] _optionProperties =
    [
        nameof(Options), nameof(ShowEntireFile), nameof(ShowNonPrintingChars), nameof(ShowSyntaxHighlighting),
        nameof(IgnoreWhitespaceEol), nameof(IgnoreWhitespaceChange), nameof(IgnoreAllWhitespace), nameof(TreatAllFilesAsText),
        nameof(CanIncreaseContext), nameof(CanDecreaseContext), nameof(IncreaseContextTip),
    ];

    private readonly IDiffService _diff;
    private readonly IAppPreferences _preferences;
    private readonly DiffViewModelServices _services;
    private readonly IReadOnlyList<Encoding> _encodings;
    private int _loadVersion;
    private IReadOnlyList<DiffLine> _lines = [];
    private bool _isLoading;
    private string? _errorMessage;

    // What is asked for: a diff read for the options, a file's text (or lines read elsewhere), or a file's git-grep matches
    // and how they are read; the file whose syntax colors the lines, and whether the lines are a diff (a first column of
    // +, - or space) or a file's text.
    private DiffRequest? _request;
    private IReadOnlyList<DiffLine>? _text;
    private IReadOnlyList<GrepLine>? _grep;
    private Func<DiffOptions, Task<(IReadOnlyList<GrepLine> Lines, string? Error)>>? _grepRead;
    private string? _syntaxPath;
    private bool _textIsDiff = true;
    private DiffOptions _options;

    // What is on show; a read that finishes after something else was asked for shows nothing.
    private int _contentVersion;
    private Func<Task>? _showLargeFile;

    private string? _selectedEncoding;
    private bool _toolbarShown;
    private Func<string, Task>? _openSettings;
    private IReadOnlyList<int> _selectedIndexes = [];
    private int _selectedIndex = -1;
    private DateTime _lastScrollReached = DateTime.MinValue;

    public DiffViewModel(IDiffService diff, IAppPreferences? preferences = null, DiffViewModelServices? services = null)
    {
        _diff = diff;
        _preferences = preferences ?? new InMemoryAppPreferences();
        _services = services ?? new DiffViewModelServices();
        _encodings = [.. _services.Encodings()];
        Encodings = [.. _encodings.Select(encoding => encoding.EncodingName)];

        // As upstream's FileViewer, each view starts from the options of this run or the saved defaults; "Treat all files as
        // text" starts off in each.
        _options = _preferences.InitialDiffOptions() with { TreatAllFilesAsText = false, Encoding = null };
        _preferences.ReloadDiffDisplayAppearance();
        ContinuousScrollHeader = _services.Translate(ContinuousScrollText);
    }

    /// <summary>
    ///  Asks the window that can stage for upstream FileViewer's stage, unstage or reset of the selected lines.
    /// </summary>
    public event EventHandler<DiffLinesCommand>? LinesRequested;

    /// <summary>
    ///  Asks the view to select and scroll to a line (an index in <see cref="Content"/>), e.g. the next change.
    /// </summary>
    public event EventHandler<int>? SelectLineRequested;

    /// <summary>
    ///  The lines of the last diff read.
    /// </summary>
    public IReadOnlyList<DiffLine> Lines
    {
        get => _lines;
        private set => SetProperty(ref _lines, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    ///  Why the view shows nothing, or less than asked for: git's error, or a binary file's note.
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    ///  What the view draws: a new instance each time the lines are to be drawn again, also when only the display changed.
    /// </summary>
    [ObservableProperty]
    public partial DiffContent Content { get; private set; } = DiffContent.Empty;

    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    /// <summary>
    ///  Upstream's <c>_NO_TRANSLATE_lblShowPreview</c>: the link that shows a large file; null while there is none.
    /// </summary>
    [ObservableProperty]
    public partial string? LargeFileText { get; private set; }

    /// <summary>
    ///  The picture on show, as <see cref="DecodeImage"/> made it; null while lines show.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShowingImage))]
    public partial object? Picture { get; private set; }

    public bool IsShowingImage => Picture is not null;

    /// <summary>
    ///  Upstream's options toolbar and menu items: for a diff and for git-grep's matches, not for a file's text.
    /// </summary>
    [ObservableProperty]
    public partial bool HasOptions { get; private set; } = true;

    /// <summary>
    ///  Set by the view: as upstream, the toolbar shows while the pointer is on the viewer.
    /// </summary>
    [ObservableProperty]
    public partial bool IsPointerOver { get; set; }

    [ObservableProperty]
    public partial bool IsToolbarVisible { get; private set; }

    [ObservableProperty]
    public partial bool IsDifftasticEnabled { get; private set; }

    /// <summary>
    ///  The line action the menu offers; it raises <see cref="LinesRequested"/>.
    /// </summary>
    public DiffLineAction LineAction { get; set; }

    /// <summary>
    ///  Upstream's <c>EnableAutomaticContinuousScroll</c>: whether the menu has the setting's item.
    /// </summary>
    public bool EnableAutomaticContinuousScroll { get; set; } = true;

    /// <summary>
    ///  Turns an image's bytes into the view's picture; it throws for bytes that are no image. Set by the view.
    /// </summary>
    public Func<byte[], object>? DecodeImage { get; set; }

    /// <summary>
    ///  Puts a text on the clipboard. Set by the view.
    /// </summary>
    public Func<string, Task>? SetClipboard { get; set; }

    /// <summary>
    ///  Opens the settings window on a page; null hides the toolbar's settings button. Set by the view.
    /// </summary>
    public Func<string, Task>? OpenSettings
    {
        get => _openSettings;
        set
        {
            if (SetProperty(ref _openSettings, value))
            {
                OnPropertyChanged(nameof(IsSettingsVisible));
            }
        }
    }

    public bool IsSettingsVisible => OpenSettings is not null;

    /// <summary>
    ///  The viewer's width in pixels, for Difftastic's columns. Set by the view.
    /// </summary>
    public double ViewerWidth { get; set; }

    /// <summary>
    ///  The whitespace, context and entire file choices in effect.
    /// </summary>
    public DiffOptions Options => _options;

    public bool ShowEntireFile
    {
        get => _options.ShowEntireFile;
        set => ChangeOptionIf(value != _options.ShowEntireFile, () => _options.ToggleEntireFile());
    }

    public bool ShowNonPrintingChars
    {
        get => _options.ShowNonPrintingChars;
        set => ChangeOptionIf(value != _options.ShowNonPrintingChars, () => _options with { ShowNonPrintingChars = value });
    }

    public bool ShowSyntaxHighlighting
    {
        get => _options.ShowSyntaxHighlighting;
        set => ChangeOptionIf(value != _options.ShowSyntaxHighlighting, () => _options with { ShowSyntaxHighlighting = value });
    }

    public bool IgnoreWhitespaceEol
    {
        get => _options.IgnoreWhitespace == IgnoreWhitespaceKind.Eol;
        set => ChangeOptionIf(value != IgnoreWhitespaceEol, () => _options.ToggleIgnoreWhitespace(IgnoreWhitespaceKind.Eol));
    }

    public bool IgnoreWhitespaceChange
    {
        get => _options.IgnoreWhitespace == IgnoreWhitespaceKind.Change;
        set => ChangeOptionIf(value != IgnoreWhitespaceChange,
            () => _options.ToggleIgnoreWhitespace(IgnoreWhitespaceKind.Change));
    }

    public bool IgnoreAllWhitespace
    {
        get => _options.IgnoreWhitespace == IgnoreWhitespaceKind.AllSpace;
        set => ChangeOptionIf(value != IgnoreAllWhitespace,
            () => _options.ToggleIgnoreWhitespace(IgnoreWhitespaceKind.AllSpace));
    }

    /// <summary>
    ///  Upstream's <c>TreatAllFilesAsTextToolStripMenuItemClick</c>: this viewer's choice only, never saved.
    /// </summary>
    public bool TreatAllFilesAsText
    {
        get => _options.TreatAllFilesAsText;
        set => ChangeOptionIf(value != _options.TreatAllFilesAsText, () => _options with { TreatAllFilesAsText = value });
    }

    // As upstream, the context buttons are off while the entire file shows.
    public bool CanIncreaseContext => !_options.ShowEntireFile;

    public bool CanDecreaseContext => !_options.ShowEntireFile && _options.ContextLines > 0;

    public string IncreaseContextTip
        => $"Increase the number of lines of context ({_options.ContextLines.ToString(CultureInfo.CurrentCulture)})";

    /// <summary>
    ///  Upstream's available encodings, by name, for the toolbar's box.
    /// </summary>
    public IReadOnlyList<string> Encodings { get; }

    /// <summary>
    ///  Upstream's <c>encodingToolStripComboBox</c>: the viewer's encoding (blank when it is none of the available ones, or
    ///  when the viewer shows no repository's file); choosing one reads the view again in it.
    /// </summary>
    public string? SelectedEncoding
    {
        get => _selectedEncoding;
        set
        {
            if (SetProperty(ref _selectedEncoding, value) && value is not null
                && _encodings.FirstOrDefault(encoding => encoding.EncodingName == value) is { } chosen)
            {
                ChangeOptions(_options with { Encoding = chosen });
            }
        }
    }

    public string ContinuousScrollHeader { get; }

    public bool IsContinuousScrollVisible => EnableAutomaticContinuousScroll;

    public bool AutomaticContinuousScroll
    {
        get => _preferences.AutomaticContinuousScroll;
        set
        {
            if (value == _preferences.AutomaticContinuousScroll)
            {
                return;
            }

            _preferences.AutomaticContinuousScroll = value;
            _preferences.Save();
            OnPropertyChanged();
        }
    }

    // Upstream's contextMenu_Opening: the line items only where the window can stage, the options only for a diff.
    public bool HasSelectedLines => _selectedIndexes.Count > 0 && IsShowingDiff;

    public bool IsStageLinesVisible => CanPatchLines && LineAction is DiffLineAction.Stage or DiffLineAction.Apply;

    public bool IsUnstageLinesVisible => CanPatchLines && LineAction == DiffLineAction.Unstage;

    public bool IsResetLinesVisible => CanPatchLines && LineAction != DiffLineAction.None;

    // Upstream's isCopyPatch: a word diff has no + and - to patch with.
    public bool IsCopyPatchVisible => !IsShowingOtherThanPatch;

    public bool AreCopyVersionsVisible => _textIsDiff && !IsShowingOtherThanPatch;

    // Upstream's isDiffAppearanceVisible: for a diff of a file.
    public bool IsDiffAppearanceVisible => HasOptions && _request is { FilePath: not null } && _text is null;

    public bool IsPatchAppearance => _preferences.DiffDisplayAppearance == DiffDisplayAppearance.Patch;

    public bool IsGitWordDiffAppearance => _preferences.DiffDisplayAppearance == DiffDisplayAppearance.GitWordDiff;

    public bool IsDifftasticAppearance => _preferences.DiffDisplayAppearance == DiffDisplayAppearance.Difftastic;

    // Upstream's isPartialTextView: the view shows a diff or git-grep's matches, not a file's text.
    public bool IsPartialTextView => IsShowingDiff || _grepRead is not null;

    /// <summary>
    ///  The file's line number at the selected line (upstream FileViewer's <c>CurrentFileLine</c>): the new number, or the old
    ///  one for a removed line; null when no numbered line is selected.
    /// </summary>
    public int? CurrentFileLine
        => _selectedIndex >= 0 && _selectedIndex < Content.Lines.Count
            ? Content.Lines[_selectedIndex].NewNumber ?? Content.Lines[_selectedIndex].OldNumber
            : null;

    private bool IsShowingDiff => _request is not null && _text is null;

    // Upstream's SupportLinePatching: a diff on show, as a patch with hunks, which a binary file's or a submodule's change
    // has not; the lines of the last diff read do not count while a file's text shows.
    private bool CanPatchLines
        => IsShowingDiff && !IsShowingOtherThanPatch && Lines.Any(line => line.Kind == DiffLineKind.Hunk);

    private bool IsShowingOtherThanPatch
        => IsShowingDiff && _preferences.DiffDisplayAppearance != DiffDisplayAppearance.Patch;

    public void ClearError() => ErrorMessage = null;

    /// <summary>
    ///  Shows an error of the view's own work (a find, a go to line) where the viewer shows its errors.
    /// </summary>
    public void ReportError(string message) => ShowError(message);

    /// <summary>
    ///  The view's selection: the selected line (the list's first) and every selected one, as indexes in
    ///  <see cref="Content"/>.
    /// </summary>
    public void SetSelection(int selectedIndex, IReadOnlyList<int> selectedIndexes)
    {
        _selectedIndex = selectedIndex;
        _selectedIndexes = selectedIndexes;
    }

    /// <summary>
    ///  Reads the menu's state again, as upstream does when its menu opens.
    /// </summary>
    public void RefreshMenu()
    {
        foreach (string name in _menuProperties)
        {
            OnPropertyChanged(name);
        }
    }

    /// <summary>
    ///  Shows the diff of <paramref name="filePath"/> in <paramref name="commitHash"/>, or of the working tree (staged or
    ///  unstaged) when no commit is given. A null file shows the whole commit.
    /// </summary>
    public async Task ShowAsync(string repositoryPath, string? commitHash, string? filePath, bool staged, string title)
    {
        int version = ResetContent();
        FileRevision revision = FileRevision.Of(commitHash, staged);
        if (filePath is not null && FileHelper.IsImage(filePath)
            && await ShowImageInsteadOfDiffAsync(version, repositoryPath, revision, filePath, title))
        {
            return;
        }

        DiffRequest request = new(repositoryPath, commitHash, filePath, staged);
        StartDiff(request, title);
        await LoadDiffAsync(request);
        await ShowNewBinaryFileAsync(version, request, revision, title);
    }

    /// <summary>
    ///  Shows the diff <paramref name="read"/> returns for the view's options (upstream's <c>FormDiff</c>, a file between
    ///  two revisions); it is read again when the options change.
    /// </summary>
    /// <param name="revision">The revision the diff goes to, whose image or new binary file is shown instead, as upstream's
    ///  <c>ViewChangesAsync</c>.</param>
    public async Task ShowDiffAsync(string repositoryPath, string? filePath, string title,
        Func<DiffOptions, Task<IReadOnlyList<DiffLine>>> read, FileRevision? revision = null)
    {
        int version = ResetContent();
        if (revision is not null && filePath is not null && FileHelper.IsImage(filePath)
            && await ShowImageInsteadOfDiffAsync(version, repositoryPath, revision, filePath, title))
        {
            return;
        }

        DiffRequest request = new(repositoryPath, CommitHash: null, filePath, Staged: false, read);
        StartDiff(request, title);
        await LoadDiffAsync(request);
        if (revision is not null)
        {
            await ShowNewBinaryFileAsync(version, request, revision, title);
        }
    }

    /// <summary>
    ///  Shows a file's content with line numbers instead of a diff, as upstream's file viewer does for the File tree tab. A
    ///  null <paramref name="content"/> is a binary file, which is not shown.
    /// </summary>
    public void ShowText(string title, string? content)
    {
        ResetContent();
        ShowFixedLines(title, content is null ? [] : DiffParser.ParseText(content), isDiff: false, syntaxPath: title,
            content is null ? BinaryFileNotShown : null);
    }

    /// <summary>
    ///  Shows lines read elsewhere, in their colors, such as a range diff's.
    /// </summary>
    public void ShowDiffLines(string title, IReadOnlyList<DiffLine> lines)
    {
        ResetContent();
        ShowFixedLines(title, lines, isDiff: false, syntaxPath: null, error: null);
    }

    /// <summary>
    ///  Shows a patch that is not in the repository (a pull request's file), as upstream's <c>ViewFixedPatch</c>.
    /// </summary>
    public void ShowPatch(string title, string patch)
    {
        ResetContent();
        ShowFixedLines(title, DiffParser.Parse(patch), isDiff: true, syntaxPath: title, error: null);
    }

    /// <summary>
    ///  Shows a file's git-grep matches as upstream's viewer does (<c>ViewGrepAsync</c>); git's error instead when it failed.
    ///  <paramref name="read"/> reads them again when the context changes.
    /// </summary>
    public Task ShowGrepAsync(string title, Func<DiffOptions, Task<(IReadOnlyList<GrepLine> Lines, string? Error)>> read)
    {
        ResetContent();
        _request = null;
        _text = null;
        _grepRead = read;
        Title = title;
        HasOptions = true;
        _syntaxPath = title;
        _textIsDiff = false;
        return LoadGrepAsync(read);
    }

    /// <summary>
    ///  Upstream's <c>ViewGitItemAsync</c>: the file of a commit, the index or the working directory, as a picture, a text
    ///  or a hex dump.
    /// </summary>
    public Task ShowFileAsync(string repositoryPath, string? commitHash, bool staged, string filePath, string? title = null)
        => LoadFileAsync(ResetContent(), repositoryPath, FileRevision.Of(commitHash, staged), filePath, title ?? filePath,
            showLarge: false);

    public void Clear()
    {
        ResetContent();
        _request = null;
        _text = null;
        _grep = null;
        _grepRead = null;
        Title = "";
        ShowError(null);
        Content = DiffContent.Empty;
    }

    /// <summary>
    ///  Loads the lines of a diff; a newer load wins, and an error is kept in <see cref="ErrorMessage"/>.
    /// </summary>
    public Task LoadAsync(string repositoryPath, string? commitHash, string? filePath, bool staged,
        DiffOptions? options = null)
        => LoadAsync(() => _diff.GetDiffAsync(repositoryPath, commitHash, filePath, staged, options));

    /// <summary>
    ///  Loads the lines <paramref name="read"/> returns (a diff between two revisions, upstream's <c>FormDiff</c>); a newer
    ///  load wins, and an error is kept in <see cref="ErrorMessage"/>.
    /// </summary>
    public async Task LoadAsync(Func<Task<IReadOnlyList<DiffLine>>> read)
    {
        int version = ++_loadVersion;
        IsLoading = true;
        try
        {
            IReadOnlyList<DiffLine> lines = await read();
            if (version == _loadVersion)
            {
                Lines = lines;
            }
        }
        catch (Exception ex)
        {
            if (version == _loadVersion)
            {
                Lines = [];
                ErrorMessage = ex.Message;
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    ///  The whole diff as git printed it, as upstream's <c>CherryPickAllChanges</c> applies it; null while no diff is shown.
    /// </summary>
    public (string Text, int Start, int Length)? WholeRange()
    {
        if (!IsShowingDiff || Lines.Count == 0 || !CanPatchLines)
        {
            return null;
        }

        string text = string.Join('\n', Lines.Select(line => line.Text)) + "\n";
        return (text, 0, text.Length);
    }

    /// <summary>
    ///  The diff as git printed it and the character range of the selected lines in it, as upstream's <c>PatchManager</c>
    ///  takes them; null when no line is selected or a file's content is shown instead of a diff.
    /// </summary>
    public (string Text, int Start, int Length)? SelectedRange()
    {
        IReadOnlyList<DiffLine> lines = Lines;
        if (!IsShowingDiff || _selectedIndexes.Count == 0 || lines.Count == 0 || !CanPatchLines)
        {
            return null;
        }

        int first = _selectedIndexes.Min();
        int last = Math.Min(_selectedIndexes.Max(), lines.Count - 1);
        StringBuilder text = new();
        int start = 0;
        int end = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (i == first)
            {
                start = text.Length;
            }

            text.Append(lines[i].Text);
            if (i == last)
            {
                end = text.Length;
            }

            text.Append('\n');
        }

        return (text.ToString(), start, end - start);
    }

    /// <summary>
    ///  Upstream's <c>IsScrollDisabled</c> and <c>IsScrollTooFast</c>: whether a wheel turn past the diff's end asks for the
    ///  next or previous file; at most once per upstream's delay, and only with Alt held unless the setting is on.
    /// </summary>
    public bool TakeScrollReached(bool altHeld)
    {
        DateTime now = _services.Now();
        if ((!altHeld && !_preferences.AutomaticContinuousScroll)
            || now - _lastScrollReached < TimeSpan.FromMilliseconds(_preferences.AutomaticContinuousScrollDelay))
        {
            return false;
        }

        _lastScrollReached = now;
        return true;
    }

    /// <summary>
    ///  Upstream's <c>PerformClickIfAvailable</c> of "Git word diff": only while the menu offers the item.
    /// </summary>
    public bool TryToggleGitWordDiff()
    {
        if (!IsDiffAppearanceVisible)
        {
            return false;
        }

        ToggleAppearance(DiffDisplayAppearance.GitWordDiff);
        return true;
    }

    /// <summary>
    ///  As <see cref="TryToggleGitWordDiff"/>, for Difftastic, which git must know.
    /// </summary>
    public bool TryToggleDifftastic()
    {
        if (!IsDiffAppearanceVisible || !IsDifftasticEnabled)
        {
            return false;
        }

        ToggleAppearance(DiffDisplayAppearance.Difftastic);
        return true;
    }

    /// <summary>
    ///  As <see cref="TryToggleGitWordDiff"/>, for syntax highlighting, which the menu offers with the options.
    /// </summary>
    public bool TryToggleSyntaxHighlighting()
    {
        if (!HasOptions)
        {
            return false;
        }

        ShowSyntaxHighlighting = !ShowSyntaxHighlighting;
        return true;
    }

    /// <summary>
    ///  As <see cref="TryToggleGitWordDiff"/>, for "Treat all files as text", which a file's text does not offer.
    /// </summary>
    public bool TryToggleTreatAllFilesAsText()
    {
        if (!IsPartialTextView)
        {
            return false;
        }

        TreatAllFilesAsText = !TreatAllFilesAsText;
        return true;
    }

    // Upstream's next and previous change buttons and hotkeys.
    [RelayCommand]
    private void GoToChange(bool next)
    {
        int? index = Content.Grep is { } grep
            ? GitGrep.FindMatch(grep, _selectedIndex, next)
            : DiffNavigation.FindChange(Content.Lines, _selectedIndex, next);
        if (index is { } line)
        {
            SelectLineRequested?.Invoke(this, line);
        }
    }

    [RelayCommand]
    private void IncreaseContext() => ChangeOptions(_options.WithMoreContext());

    [RelayCommand]
    private void DecreaseContext() => ChangeOptions(_options.WithLessContext());

    // Upstream's ResetPatchAppearance: the patch, which the parent item resets to too.
    [RelayCommand]
    private void ChangeAppearance(DiffDisplayAppearance appearance)
    {
        // Upstream's OnExtraDiffArgumentsChanged: for every viewer, and this one reads its diff again.
        _preferences.DiffDisplayAppearance = appearance;
        RefreshMenu();
        Reload();
    }

    // Upstream's ToggleGitWordColoring and ToggleDifftastic: the one on goes back to the patch.
    [RelayCommand]
    private void ToggleAppearance(DiffDisplayAppearance appearance)
        => ChangeAppearance(_preferences.DiffDisplayAppearance == appearance ? DiffDisplayAppearance.Patch : appearance);

    [RelayCommand]
    private void RequestLines(DiffLinesCommand command) => LinesRequested?.Invoke(this, command);

    // The selected lines in order, or all lines when none is selected, as upstream copies.
    [RelayCommand]
    private Task CopyAsync() => CopyTextAsync(DiffCopy.Text(SelectedOrAllLines(), Content.IsDiff));

    [RelayCommand]
    private Task CopyPatchAsync() => CopyTextAsync(DiffCopy.Patch(SelectedOrAllLines()));

    [RelayCommand]
    private Task CopyNewVersionAsync()
        => CopyTextAsync(DiffCopy.Version(SelectedOrAllLines(), Content.IsDiff, newVersion: true));

    [RelayCommand]
    private Task CopyOldVersionAsync()
        => CopyTextAsync(DiffCopy.Version(SelectedOrAllLines(), Content.IsDiff, newVersion: false));

    // Upstream's settingsButton_Click: the Diff viewer page of the settings.
    [RelayCommand]
    private Task ShowSettingsAsync()
        => OpenSettings is { } open ? GuardAsync(() => open(DiffViewerSettingsPage)) : Task.CompletedTask;

    [RelayCommand]
    private Task ShowLargeFileAsync() => _showLargeFile is { } show ? GuardAsync(show) : Task.CompletedTask;

    partial void OnIsPointerOverChanged(bool value) => UpdateToolbar();

    partial void OnHasOptionsChanged(bool value) => UpdateToolbar();

    private void UpdateToolbar()
    {
        bool visible = HasOptions && IsPointerOver;

        // Upstream's fileviewerToolbar_VisibleChanged: the encoding box shows the viewer's encoding.
        if (visible && !_toolbarShown)
        {
            ShowEncoding();
        }

        _toolbarShown = visible;
        IsToolbarVisible = visible;
    }

    private void ShowEncoding()
    {
        Encoding? encoding = _options.Encoding
                             ?? (_request?.RepositoryPath is { } path ? _services.FilesEncoding(path) : null);
        _selectedEncoding = encoding?.EncodingName is { } name && Encodings.Contains(name) ? name : null;
        OnPropertyChanged(nameof(SelectedEncoding));
    }

    private void ChangeOptionIf(bool changed, Func<DiffOptions> change)
    {
        if (changed)
        {
            ChangeOptions(change());
        }
    }

    // As upstream's FileViewer: the change is kept for the next view in this run and the diff is read again.
    private void ChangeOptions(DiffOptions options)
    {
        if (options == _options)
        {
            return;
        }

        // Nonprinting characters and syntax highlighting change only the display; the diff is not read again for them.
        bool sameDiff = options.Arguments.SequenceEqual(_options.Arguments) && Equals(options.Encoding, _options.Encoding);
        _options = options;
        _preferences.SetDiffOptions(options);
        foreach (string name in _optionProperties)
        {
            OnPropertyChanged(name);
        }

        if (sameDiff)
        {
            Content = Content.Redrawn();
        }
        else
        {
            Reload();
        }
    }

    private void Reload()
    {
        if (_grepRead is { } grepRead)
        {
            Run(() => LoadGrepAsync(grepRead));
            return;
        }

        if (_request is { } request)
        {
            Run(() => LoadDiffAsync(request));
        }
    }

    private void StartDiff(DiffRequest request, string title)
    {
        _request = request;
        _text = null;
        _grep = null;
        _grepRead = null;
        _syntaxPath = request.FilePath;
        _textIsDiff = true;
        HasOptions = true;
        Title = title;
    }

    private async Task LoadDiffAsync(DiffRequest request)
    {
        ShowError(null);
        DiffOptions options = _options with
        {
            Histogram = _preferences.UseHistogramDiffAlgorithm,
            UseGitColoring = _preferences.UseGitColoring,
            Appearance = _preferences.DiffDisplayAppearance,
            DifftasticWidth = Difftastic.WidthFor(ViewerWidth),
        };
        ShowDifftasticEnabled(request.RepositoryPath);
        await (request.Read is { } read
            ? LoadAsync(() => read(options))
            : LoadAsync(request.RepositoryPath, request.CommitHash, request.FilePath, request.Staged, options));
        if (_request == request)
        {
            ShowContent();
        }
    }

    private async Task LoadGrepAsync(Func<DiffOptions, Task<(IReadOnlyList<GrepLine> Lines, string? Error)>> read)
    {
        (IReadOnlyList<GrepLine> lines, string? error) = await read(_options);
        if (_grepRead != read)
        {
            return;
        }

        ShowError(error);
        _grep = lines;
        ShowContent();
    }

    private void ShowFixedLines(string title, IReadOnlyList<DiffLine> lines, bool isDiff, string? syntaxPath, string? error)
    {
        _request = null;
        _grep = null;
        _grepRead = null;
        Title = title;
        HasOptions = false;
        ShowError(error);
        _syntaxPath = syntaxPath;
        _textIsDiff = isDiff;
        _text = lines;
        ShowContent();
    }

    // A file's git-grep matches are lines with their numbers in the file, which copying takes as a file's text.
    private void ShowContent()
        => Content = _grep is { } grep
            ? new DiffContent([.. grep.Select(line => new DiffLine(line.Text, DiffLineKind.Context, NewNumber: line.Number))],
                grep, isDiff: false, _syntaxPath)
            : new DiffContent(_text ?? Lines, grep: null, _textIsDiff, _syntaxPath);

    private void ShowError(string? message) => ErrorMessage = message;

    // Upstream's SetDifftasticEnabled.
    private void ShowDifftasticEnabled(string repositoryPath)
    {
        if (_difftasticEnabled.TryGetValue(repositoryPath, out bool enabled))
        {
            IsDifftasticEnabled = enabled;
            return;
        }

        Run(async () =>
        {
            bool found = await Task.Run(() => _services.IsDifftasticEnabled(repositoryPath));
            _difftasticEnabled[repositoryPath] = found;
            IsDifftasticEnabled = found;
        });
    }

    private IReadOnlyList<DiffLine> SelectedOrAllLines()
    {
        IReadOnlyList<DiffLine> lines = Content.Lines;
        return _selectedIndexes.Count == 0
            ? lines
            : [.. _selectedIndexes.Where(index => index < lines.Count).Order().Select(index => lines[index])];
    }

    private Task CopyTextAsync(string text)
        => text.Length > 0 && SetClipboard is { } setClipboard ? GuardAsync(() => setClipboard(text)) : Task.CompletedTask;

    // Upstream's ResetView: whatever was shown goes, and reads still running no longer count.
    private int ResetContent()
    {
        _showLargeFile = null;
        LargeFileText = null;
        object? shown = Picture;
        Picture = null;
        (shown as IDisposable)?.Dispose();
        return ++_contentVersion;
    }

    private async Task LoadFileAsync(int version, string repositoryPath, FileRevision revision, string filePath, string title,
        bool showLarge)
    {
        ViewedFile file = await _services.ReadFile(repositoryPath, revision, filePath, _options, showLarge);
        if (version == _contentVersion)
        {
            await ShowViewedFileAsync(version, file, repositoryPath, revision, filePath, title);
        }
    }

    // Upstream's ViewChangesAsync: an image that the revision has shows as a picture, not as a diff. False when it has none,
    // as for a deleted image, which keeps its diff.
    private async Task<bool> ShowImageInsteadOfDiffAsync(int version, string repositoryPath, FileRevision revision,
        string filePath, string title)
    {
        ViewedFile file = await _services.ReadFile(repositoryPath, revision, filePath, _options, false);
        if (version != _contentVersion)
        {
            return true;
        }

        if (file.Error is not null)
        {
            return false;
        }

        await ShowViewedFileAsync(version, file, repositoryPath, revision, filePath, title);
        return true;
    }

    // Upstream's ViewChangesAsync for a new file, here only a binary one: its hex dump, not git's one line about it.
    private async Task ShowNewBinaryFileAsync(int version, DiffRequest request, FileRevision revision, string title)
    {
        if (version == _contentVersion && _request == request && request.FilePath is { } filePath
            && Lines.Any(line => line.Text.StartsWith(NewBinaryFilePrefix, StringComparison.Ordinal)))
        {
            await LoadFileAsync(ResetContent(), request.RepositoryPath, revision, filePath, title, showLarge: false);
        }
    }

    private async Task ShowViewedFileAsync(int version, ViewedFile file, string repositoryPath, FileRevision revision,
        string filePath, string title)
    {
        if (file.TooLarge is { } length)
        {
            ShowContentText(title, "", syntaxPath: null);
            LargeFileText = string.Format(CultureInfo.CurrentCulture, _services.Translate(FileContent.LargeFileSizeWarning),
                length / BytesPerMb);
            _showLargeFile = () => LoadFileAsync(ResetContent(), repositoryPath, revision, filePath, title, showLarge: true);
        }
        else if (file.Image is { } image)
        {
            await ShowImageAsync(version, image, title, filePath);
        }
        else if (file.BinaryData is { } data)
        {
            ShowContentText(title, FileContent.HexDump(FileContent.BinaryFile, filePath, data, _services.Translate),
                syntaxPath: null);
        }
        else if (file.Text is { } text)
        {
            ShowContentText(title, text, filePath);
        }
        else
        {
            ShowContentText(title, "", syntaxPath: null);
            ShowError(file.Error);
        }
    }

    // Upstream's ViewItemAsync: the picture as the view decodes it; bytes it cannot decode show as upstream's "Cannot view
    // image" with their hex dump.
    private async Task ShowImageAsync(int version, byte[] image, string title, string filePath)
    {
        object picture;
        try
        {
            Func<byte[], object> decode = DecodeImage ?? throw new InvalidOperationException("No image decoder.");
            picture = await Task.Run(() => decode(image));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (version == _contentVersion)
            {
                ShowContentText(title, FileContent.HexDump(FileContent.CannotViewImage, filePath,
                    FileContent.BinaryData(image), _services.Translate), syntaxPath: null);
            }

            return;
        }

        if (version != _contentVersion)
        {
            (picture as IDisposable)?.Dispose();
            return;
        }

        ShowContentText(title, "", syntaxPath: null);
        Picture = picture;
    }

    private void ShowContentText(string title, string text, string? syntaxPath)
        => ShowFixedLines(title, DiffParser.ParseText(text), isDiff: false, syntaxPath, error: null);

    private void Run(Func<Task> action) => _ = GuardAsync(action);

    // An exception that escapes an asynchronous command ends the app; it shows in the viewer instead, as the view did.
    private async Task GuardAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // Nothing to report.
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private sealed record DiffRequest(string RepositoryPath, string? CommitHash, string? FilePath, bool Staged,
        Func<DiffOptions, Task<IReadOnlyList<DiffLine>>>? Read = null);
}

/// <summary>
///  What a diff viewer draws: its lines (a file's git-grep matches as lines with their numbers), git-grep's own lines when
///  it shows them, whether the lines are a diff and the file whose syntax colors them. A class, so that each instance is
///  new to a binding: the same lines are drawn again when only the display changed.
/// </summary>
public sealed class DiffContent(IReadOnlyList<DiffLine> lines, IReadOnlyList<GrepLine>? grep, bool isDiff, string? syntaxPath)
{
    public static DiffContent Empty => new([], grep: null, isDiff: true, syntaxPath: null);

    public IReadOnlyList<DiffLine> Lines { get; } = lines;

    public IReadOnlyList<GrepLine>? Grep { get; } = grep;

    public bool IsDiff { get; } = isDiff;

    public string? SyntaxPath { get; } = syntaxPath;

    /// <summary>
    ///  The same content, to be drawn again.
    /// </summary>
    public DiffContent Redrawn() => new(Lines, Grep, IsDiff, SyntaxPath);
}

/// <summary>
///  What a diff viewer's view model reads outside itself: tests replace the reads that run git.
/// </summary>
public sealed record DiffViewModelServices
{
    /// <summary>
    ///  Upstream's <c>ViewGitItemAsync</c> read: the file of a revision, the last argument to read even a large one.
    /// </summary>
    public Func<string, FileRevision, string, DiffOptions, bool, Task<ViewedFile>> ReadFile { get; init; }
        = (repositoryPath, revision, path, options, showLarge)
            => FileContent.ReadAsync(repositoryPath, revision, path, options, showLarge);

    public Func<string, bool> IsDifftasticEnabled { get; init; }
        = repositoryPath => Difftastic.IsEnabled(GitModules.Open(repositoryPath));

    /// <summary>
    ///  The repository's files encoding (upstream's <c>Module.FilesEncoding</c>).
    /// </summary>
    public Func<string, Encoding?> FilesEncoding { get; init; } = repositoryPath => GitModules.Open(repositoryPath).FilesEncoding;

    /// <summary>
    ///  Upstream's available encodings (<c>AppSettings.AvailableEncodings</c>), read when the viewer is made.
    /// </summary>
    public Func<IEnumerable<Encoding>> Encodings { get; init; } = () => AppSettings.AvailableEncodings.Values;

    /// <summary>
    ///  Upstream's translation of an English text.
    /// </summary>
    public Func<string, string> Translate { get; init; } = text => text;

    public Func<DateTime> Now { get; init; } = () => DateTime.Now;
}

/// <summary>
///  Upstream FileViewer's line commands, which the window that can stage carries out.
/// </summary>
public enum DiffLinesCommand
{
    Stage,
    Unstage,
    Reset,
}
