using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitExtensions.Xplat.Core.Editing;

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
    public EditorWindow(string path, bool showWarning = false)
    {
        ViewModel = new EditorViewModel(new WindowDialogHost(this), path, showWarning);
        _path = ViewModel.Path;
        DataContext = ViewModel;
        InitializeComponent();
        ContentBox.NewLine = "\n";

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
        ContentBox.CaretIndex = 0;
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

    private void Run(Func<Task> action) => UiActions.Run(action, ex => ViewModel.ErrorText = ex.Message);
}
