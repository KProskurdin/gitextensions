using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitExtensions.Xplat.Core.Editing;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Edits one text file: the new shell's version of upstream <c>FormEditor</c>. The app opens it for the <c>fileeditor</c>
///  verb, which is how git hands over the rebase todo list and the messages of reworded or squashed commits
///  (see <see cref="Core.Operations.GitEditorCommand"/>). Closing with unsaved changes asks whether to save them, as upstream.
/// </summary>
public partial class EditorWindow : Window
{
    private readonly string _path;
    private EditorFile? _file;
    private string _savedText = "";
    private bool _closeDecided;

    public EditorWindow(string path)
    {
        _path = Path.GetFullPath(path);
        InitializeComponent();
        Title = _path;
        ContentBox.NewLine = "\n";
        ContentBox.IsReadOnly = true;
        SaveButton.IsEnabled = false;
        SaveButton.Click += (_, _) => Run(SaveAsync);
        SaveAndCloseButton.Click += (_, _) => Run(SaveAndCloseAsync);
        DiscardButton.Click += (_, _) => CloseWith(accepted: false);
        KeepEditingButton.Click += (_, _) => SaveChangesBar.IsVisible = false;
        ContentBox.TextChanged += (_, _) => SaveButton.IsEnabled = HasChanges;
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        Closing += OnClosing;
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  True when the window closed with the file saved or left unchanged; false when the changes were discarded or the
    ///  file could not be read. The app exits with upstream's code for it (0 or -1), which tells git whether to go on.
    /// </summary>
    public bool Accepted { get; private set; }

    /// <summary>
    ///  Completes when the file has been read into the window.
    /// </summary>
    public Task FileLoaded { get; private set; } = Task.CompletedTask;

    private bool HasChanges => _file is not null && (ContentBox.Text ?? "") != _savedText;

    private Task LoadAsync() => FileLoaded = LoadFileAsync();

    // A file that does not exist yet opens empty and is created on save, so the verb also works for a new file.
    private async Task LoadFileAsync()
    {
        _file = File.Exists(_path)
            ? await EditorFile.ReadAsync(_path)
            : new EditorFile("", "\n", HasByteOrderMark: false);
        _savedText = _file.Text;
        ContentBox.Text = _file.Text;
        ContentBox.IsReadOnly = false;
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

    private async Task SaveAsync()
    {
        if (_file is null)
        {
            return;
        }

        string text = ContentBox.Text ?? "";
        await _file.WriteAsync(_path, text);
        _savedText = text;
        SaveButton.IsEnabled = HasChanges;
        ErrorText.Text = "";
    }

    private async Task SaveAndCloseAsync()
    {
        await SaveAsync();
        CloseWith(accepted: true);
    }

    private void CloseWith(bool accepted)
    {
        _closeDecided = true;
        Accepted = accepted;
        Close();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeDecided)
        {
            return;
        }

        if (HasChanges)
        {
            e.Cancel = true;
            SaveChangesBar.IsVisible = true;
            SaveAndCloseButton.Focus();
            return;
        }

        Accepted = _file is not null;
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
            Run(SaveAsync);
        }
    }

    private void Run(Func<Task> action) => UiActions.Run(action, ex => ErrorText.Text = ex.Message);
}
