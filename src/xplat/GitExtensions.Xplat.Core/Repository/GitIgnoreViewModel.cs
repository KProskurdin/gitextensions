using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The view model of upstream's <c>FormGitIgnore</c> (rules in <see cref="GitIgnoreDialog"/>): edits the repository's
///  .gitignore or its local excludes, with upstream's default ignores, "Add pattern", and its question when the window
///  closes with unsaved changes (<see cref="MayClose"/>, <see cref="AskToCloseAsync"/>). <see cref="Saved"/> tells whether
///  the file was written.
/// </summary>
public sealed partial class GitIgnoreViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormGitIgnore";

    private const string HelpTextName = "label1";

    private readonly string _excludeFile;
    private readonly GitIgnoreTexts _texts;
    private readonly Func<bool> _isBare;
    private readonly Func<Task<bool>> _addPattern;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string _savedText = "";
    private bool _closeDecided;

    /// <param name="excludeFile">The file edited: the repository's .gitignore or .git/info/exclude.</param>
    /// <param name="localExclude">True for .git/info/exclude, whose texts upstream words differently.</param>
    /// <param name="isBare">Whether the repository is bare, which has no file to edit.</param>
    /// <param name="addPattern">Upstream's <c>FormAddToGitIgnore</c> with its proposed pattern; true when it added.</param>
    public GitIgnoreViewModel(IDialogHost host, string excludeFile, bool localExclude, Func<bool> isBare,
        Func<Task<bool>> addPattern)
        : base(host)
    {
        _excludeFile = excludeFile;
        _texts = GitIgnoreDialog.TextsFor(localExclude);
        _isBare = isBare;
        _addPattern = addPattern;
        Title = Host.Translate(_texts.Caption);
        HelpText = Host.Translate(Category, HelpTextName, GitIgnoreDialog.HelpText);
    }

    public string Title { get; }

    /// <summary>
    ///  Upstream's help about patterns, beside the editor.
    /// </summary>
    public string HelpText { get; }

    [ObservableProperty]
    public partial string? Text { get; set; }

    /// <summary>
    ///  The file's line ending, which the editor keeps.
    /// </summary>
    [ObservableProperty]
    public partial string NewLine { get; private set; } = Environment.NewLine;

    /// <summary>
    ///  True once the file is read: the editor can be typed in and the buttons used.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadOnly))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(AddDefaultsCommand), nameof(AddPatternCommand))]
    public partial bool IsLoaded { get; private set; }

    public bool IsReadOnly => !IsLoaded;

    /// <summary>
    ///  True when the file was written while the window was open (by Save, the closing question or "Add pattern").
    /// </summary>
    public bool Saved { get; private set; }

    /// <summary>
    ///  Completes when the file has been read into the editor.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  True when the window may close without a question: nothing unsaved, or the question was answered.
    /// </summary>
    public bool MayClose => _closeDecided || !HasUnsavedChanges();

    /// <summary>
    ///  Upstream's <c>FormGitIgnoreLoad</c> (a bare repository has no file to edit) and <c>LoadGitIgnore</c>. The window
    ///  calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        if (await Task.Run(_isBare))
        {
            await Host.ShowMessageAsync(Host.Translate(_texts.OnlyInWorkingDir),
                Host.Translate(GitIgnoreDialog.NoWorkingDirCaption));
            _closeDecided = true;
            Host.Close(null);
            return;
        }

        await ReloadAsync();
        IsLoaded = true;
        _loaded.TrySetResult();
    });

    /// <summary>
    ///  Upstream's <c>FormGitIgnoreFormClosing</c>: unsaved changes are saved, dropped or kept open as the question's answer
    ///  says; the window is closed again unless kept open.
    /// </summary>
    public Task AskToCloseAsync() => GuardAsync(async () =>
    {
        bool? save = await Host.AskAsync(Host.Translate(_texts.SaveQuestion),
            Host.Translate(GitIgnoreDialog.SaveQuestionCaption));
        if (save is null || (save == true && !await TrySaveAsync()))
        {
            return;
        }

        _closeDecided = true;
        Host.Close(null);
    });

    // Upstream's SaveClick: save, then close, which asks again when the save failed.
    [RelayCommand(CanExecute = nameof(IsLoaded))]
    private Task SaveAsync() => GuardAsync(async () =>
    {
        await TrySaveAsync();
        Host.Close(null);
    });

    [RelayCommand]
    private void Cancel() => Host.Close(null);

    // Upstream's AddDefaultClick: the default patterns the file does not have yet, unsaved.
    [RelayCommand(CanExecute = nameof(IsLoaded))]
    private Task AddDefaultsAsync() => GuardAsync(async () =>
    {
        if (GitIgnoreDialog.AddDefaults(Text ?? "", await GitIgnoreDialog.DefaultPatternsAsync()) is { } text)
        {
            Text = text;
        }
    });

    // Upstream's AddPattern_Click: the changes saved, the add window with its proposed pattern, and the file read again.
    [RelayCommand(CanExecute = nameof(IsLoaded))]
    private Task AddPatternAsync() => GuardAsync(async () =>
    {
        await TrySaveAsync();
        Saved |= await _addPattern();
        await ReloadAsync();
    });

    private async Task ReloadAsync()
    {
        string text = await GitIgnoreDialog.ReadAsync(_excludeFile) ?? "";
        NewLine = GitIgnoreDialog.NewLineOf(text);
        Text = text;
        _savedText = text;
    }

    // Upstream's SaveGitIgnore: nothing to do without changes; a failure is shown and leaves the changes unsaved.
    private async Task<bool> TrySaveAsync()
    {
        if (!HasUnsavedChanges())
        {
            return false;
        }

        try
        {
            string text = Text ?? "";
            _savedText = await Task.Run(() => GitIgnoreDialog.Save(_excludeFile, text));
            if (_savedText != text)
            {
                Text = _savedText;
            }

            Saved = true;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await Host.ShowMessageAsync(Host.Translate(_texts.CannotAccess) + Environment.NewLine + ex.Message,
                Host.Translate(_texts.CannotAccessCaption));
            return false;
        }
    }

    private bool HasUnsavedChanges() => _savedText != (Text ?? "");
}
