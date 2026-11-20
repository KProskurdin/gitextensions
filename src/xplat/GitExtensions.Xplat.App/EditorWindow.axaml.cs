using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Editing;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Edits one text file: the new shell's version of upstream <c>FormEditor</c>, bound to <see cref="EditorViewModel"/>. The
///  app opens it for the <c>fileeditor</c> verb, which is how git hands over the rebase todo list and the messages of
///  reworded or squashed commits (see <see cref="Core.Operations.GitEditorCommand"/>). Closing with unsaved changes asks
///  whether to save them, as upstream. Find and replace work on the text box (<c>EditorWindow.Find.cs</c>).
/// </summary>
public partial class EditorWindow : Window
{
    private readonly string _path;
    private readonly int? _lineNumber;

    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public EditorWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="path">The file to edit.</param>
    /// <param name="showWarning">
    ///  Shows upstream's warning that changing the file by hand can break something (its <c>showWarning</c>, for
    ///  .git/config).
    /// </param>
    /// <param name="lineNumber">Upstream's <c>lineNumber</c>: the line the caret starts on, from 1.</param>
    public EditorWindow(string path, bool showWarning = false, int? lineNumber = null)
    {
        _lineNumber = lineNumber;
        ViewModel = new EditorViewModel(new WindowDialogHost(this), path, showWarning);
        _path = ViewModel.Path;
        DataContext = ViewModel;
        InitializeComponent();

        // View concerns: the keys (Escape, the save shortcut and find's), the editor menu, the question's focus, and the
        // caret once the file is read.
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        AddEditorMenu();
        Closing += (_, e) =>
        {
            if (ViewModel.OnClosing())
            {
                e.Cancel = true;
                SaveAndCloseButton.Focus();
            }
        };
        Opened += (_, _) => FileLoaded = ShowFileAsync();
    }

    public EditorViewModel ViewModel { get; }

    /// <summary>
    ///  True when the window closed with the file saved or left unchanged; false when the changes were discarded or the
    ///  file could not be read. The app exits with upstream's code for it (0 or -1), which tells git whether to go on.
    /// </summary>
    public bool Accepted => ViewModel.Accepted;

    /// <summary>
    ///  Completes when the file has been read into the window.
    /// </summary>
    public Task FileLoaded { get; private set; } = Task.CompletedTask;

    private async Task ShowFileAsync()
    {
        await ViewModel.LoadAsync();
        ShowText();
        string text = ContentBox.Text ?? "";
        ContentBox.CaretOffset = _lineNumber is { } line
            ? TextReplace.OffsetOfLine(text, Math.Clamp(line, 1, TextReplace.LineOf(text, text.Length)))
            : 0;
        ContentBox.TextArea.Caret.BringCaretToView();
        ContentBox.Focus();

        // With the screenshot aid the file is shown, saved as a picture and accepted unchanged, so a script can run the app
        // as git's editor.
        if (Screenshot.RequestedFile is { } screenshot)
        {
            await Screenshot.SaveAsync(this, screenshot);
            Close();
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.S && e.KeyModifiers.HasFlag(Hotkeys.CommandModifier))
        {
            e.Handled = true;
            ViewModel.SaveCommand.Execute(null);
        }
        else if (RunFindHotkey(e))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    ///  Upstream's highlighting of the file, once it is shown; null for a file with no syntax.
    /// </summary>
    public EditorColorizer? Colorizer { get; private set; }

    // The editor's text is not bindable: it starts as the file's, and each change goes to the view model. Upstream's
    // highlighting follows the text (EditorColorizer): git's files with the repository's comment character, others in the
    // syntax of their extension; a commit message's waves show their tooltip under the pointer.
    private void ShowText()
    {
        ContentBox.Text = ViewModel.Text ?? "";
        EditorSyntax syntax = EditorHighlighting.SyntaxOf(_path);
        SyntaxDefinition? definition = syntax == EditorSyntax.File ? SyntaxDefinitions.ForFile(_path) : null;
        if (syntax != EditorSyntax.File || definition is not null)
        {
            EditorColorizer colorizer = new(syntax,
                syntax == EditorSyntax.File ? EditorHighlighting.DefaultCommentChar
                    : EditorHighlighting.CommentChar(CommentCharSetting(_path)), definition);
            colorizer.Update(ContentBox.Document);
            ContentBox.TextArea.TextView.LineTransformers.Add(colorizer);
            ContentBox.TextArea.TextView.BackgroundRenderers.Add(colorizer.WaveRenderer);
            ContentBox.Document.Changed += (_, _) =>
            {
                colorizer.Update(ContentBox.Document);
                ContentBox.TextArea.TextView.Redraw();
            };
            ContentBox.PointerHover += (_, e) =>
            {
                if (ContentBox.GetPositionFromPoint(e.GetPosition(ContentBox)) is { } position
                    && colorizer.ToolTipAt(ContentBox.Document, ContentBox.Document.GetOffset(position.Location)) is { } tip)
                {
                    ToolTip.SetTip(ContentBox, tip);
                    ToolTip.SetIsOpen(ContentBox, true);
                }
            };
            ContentBox.PointerHoverStopped += (_, _) => ToolTip.SetIsOpen(ContentBox, false);
            Colorizer = colorizer;
        }

        ContentBox.TextChanged += (_, _) => ViewModel.Text = ContentBox.Text;
    }

    // Upstream's core.commentchar of the repository the file belongs to (git's files are in its .git folder).
    private static string? CommentCharSetting(string path)
    {
        try
        {
            return GitModules.Open(Path.GetDirectoryName(path) ?? "").GetEffectiveSetting("core.commentchar");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    private void Run(Func<Task> action) => UiActions.Run(action, ex => ViewModel.ErrorText = ex.Message);
}
