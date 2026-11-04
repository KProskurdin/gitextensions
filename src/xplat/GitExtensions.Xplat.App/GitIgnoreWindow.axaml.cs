using Avalonia.Controls;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormGitIgnore</c>: edits the repository's .gitignore or its local excludes (.git/info/exclude), with
///  upstream's default ignores, "Add pattern" (<see cref="AddToGitIgnoreWindow"/>), help text and links, and its question
///  when the window closes with unsaved changes. <see cref="Saved"/> tells whether the file was written.
/// </summary>
public partial class GitIgnoreWindow : Window
{
    private const string FormName = "FormGitIgnore";
    private const string HelpTextName = "label1";
    private const string TextProperty = "Text";

    private readonly string _repositoryPath;
    private readonly bool _localExclude;
    private readonly GitIgnoreTexts _texts;
    private readonly string _excludeFile;
    private readonly TaskCompletionSource _loaded = new();
    private string _savedText = "";
    private bool _closeDecided;

    public GitIgnoreWindow()
        : this("", localExclude: false)
    {
    }

    public GitIgnoreWindow(string repositoryPath, bool localExclude = false)
    {
        _repositoryPath = repositoryPath;
        _localExclude = localExclude;
        _texts = GitIgnoreDialog.TextsFor(localExclude);
        _excludeFile = repositoryPath.Length > 0 ? GitIgnoreFile.PathIn(repositoryPath, localExclude) : "";
        InitializeComponent();
        UpstreamTranslation.Apply(this, FormName);
        InitialFocus.Attach(this, () => _NO_TRANSLATE_GitIgnoreEdit);
        Title = UpstreamTranslation.ByText(_texts.Caption);
        label1.Text = UpstreamTranslation.Find(FormName, HelpTextName, TextProperty) ?? GitIgnoreDialog.HelpText;
        SetButtonsEnabled(false);
        SaveButton.Click += (_, _) => Run(SaveAndCloseAsync);
        btnCancel.Click += (_, _) => Close();
        AddDefault.Click += (_, _) => Run(AddDefaultsAsync);
        AddPattern.Click += (_, _) => Run(AddPatternAsync);
        lnkGitIgnorePatterns.Click += (_, _) => OsShellUtil.OpenUrlInDefaultBrowser(GitIgnoreDialog.PatternsUrl);
        lnkGitIgnoreGenerate.Click += (_, _) => OsShellUtil.OpenUrlInDefaultBrowser(GitIgnoreDialog.GenerateUrl);
        Closing += OnClosing;
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  True when the file was written while the window was open (by Save, the closing question or "Add pattern").
    /// </summary>
    public bool Saved { get; private set; }

    /// <summary>
    ///  Completes when the file has been read into the editor.
    /// </summary>
    public Task FileLoaded => _loaded.Task;

    private string EditorText => _NO_TRANSLATE_GitIgnoreEdit.Text ?? "";

    /// <summary>
    ///  Upstream's <c>StartEditGitIgnoreDialog</c>: the window shown over <paramref name="owner"/>; true when the file was
    ///  saved.
    /// </summary>
    public static async Task<bool> ShowAsync(Window owner, string repositoryPath, bool localExclude)
    {
        GitIgnoreWindow window = new(repositoryPath, localExclude);
        await window.ShowDialog(owner);
        return window.Saved;
    }

    // Upstream's FormGitIgnoreLoad (a bare repository has no file to edit) and LoadGitIgnore.
    private async Task LoadAsync()
    {
        if (await Task.Run(() => GitIgnoreDialog.IsBare(_repositoryPath)))
        {
            await new MessageBoxWindow(UpstreamTranslation.ByText(_texts.OnlyInWorkingDir),
                UpstreamTranslation.ByText(GitIgnoreDialog.NoWorkingDirCaption), MessageBoxButtons.OK).ShowDialog(this);
            _closeDecided = true;
            Close();
            return;
        }

        await ReloadAsync();
        SetButtonsEnabled(true);
        _NO_TRANSLATE_GitIgnoreEdit.Focus();
        _loaded.TrySetResult();
    }

    private async Task ReloadAsync()
    {
        string text = await GitIgnoreDialog.ReadAsync(_excludeFile) ?? "";
        _NO_TRANSLATE_GitIgnoreEdit.NewLine = GitIgnoreDialog.NewLineOf(text);
        _NO_TRANSLATE_GitIgnoreEdit.Text = text;
        _NO_TRANSLATE_GitIgnoreEdit.IsReadOnly = false;
        _savedText = text;
    }

    private void SetButtonsEnabled(bool enabled)
    {
        SaveButton.IsEnabled = enabled;
        AddDefault.IsEnabled = enabled;
        AddPattern.IsEnabled = enabled;
    }

    // Upstream's SaveClick: save, then close, which asks again when the save failed.
    private async Task SaveAndCloseAsync()
    {
        await TrySaveAsync();
        Close();
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
            string text = EditorText;
            _savedText = await Task.Run(() => GitIgnoreDialog.Save(_excludeFile, text));
            if (_savedText != text)
            {
                _NO_TRANSLATE_GitIgnoreEdit.Text = _savedText;
            }

            Saved = true;
            return true;
        }
        catch (Exception ex)
        {
            await new MessageBoxWindow(
                UpstreamTranslation.ByText(_texts.CannotAccess) + Environment.NewLine + ex.Message,
                UpstreamTranslation.ByText(_texts.CannotAccessCaption), MessageBoxButtons.OK).ShowDialog(this);
            return false;
        }
    }

    // Upstream's AddDefaultClick: the default patterns the file does not have yet, unsaved.
    private async Task AddDefaultsAsync()
    {
        if (GitIgnoreDialog.AddDefaults(EditorText, await GitIgnoreDialog.DefaultPatternsAsync()) is { } text)
        {
            _NO_TRANSLATE_GitIgnoreEdit.Text = text;
        }
    }

    // Upstream's AddPattern_Click: the changes saved, the add window with its proposed pattern, and the file read again.
    private async Task AddPatternAsync()
    {
        await TrySaveAsync();
        AddToGitIgnoreWindow add = new(_repositoryPath, _localExclude, [GitIgnoreDialog.ProposedPattern]);
        await add.ShowDialog(this);
        Saved |= add.Added;
        await ReloadAsync();
    }

    private bool HasUnsavedChanges() => _savedText != EditorText;

    // Upstream's FormGitIgnoreFormClosing: unsaved changes are saved, dropped or kept open as the question's answer says.
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeDecided || !HasUnsavedChanges())
        {
            return;
        }

        e.Cancel = true;
        Run(AskAndCloseAsync);
    }

    private async Task AskAndCloseAsync()
    {
        MessageBoxWindow question = new(UpstreamTranslation.ByText(_texts.SaveQuestion),
            UpstreamTranslation.ByText(GitIgnoreDialog.SaveQuestionCaption), MessageBoxButtons.YesNoCancel);
        await question.ShowDialog(this);
        if (question.Result == DialogResult.Cancel
            || (question.Result == DialogResult.Yes && !await TrySaveAsync()))
        {
            return;
        }

        _closeDecided = true;
        Close();
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}
