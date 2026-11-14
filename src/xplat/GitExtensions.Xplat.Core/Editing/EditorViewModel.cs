using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.Core.Editing;

/// <summary>
///  The view model of upstream's <c>FormEditor</c>: one text file read in its repository's files encoding, saved as it
///  was read (line endings and byte order mark), and a question about unsaved changes on close
///  (<see cref="OnClosing"/>). <see cref="Accepted"/> is what the app tells git.
/// </summary>
public sealed partial class EditorViewModel : DialogViewModel
{
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private EditorFile? _file;
    private string _savedText = "";
    private readonly Func<string, Encoding> _filesEncoding;
    private bool _closeDecided;

    /// <param name="path">The file to edit.</param>
    /// <param name="showWarning">Upstream's <c>showWarning</c>: changing the file by hand can break something.</param>
    /// <param name="filesEncoding">The files encoding of the repository a folder is in (upstream's
    /// <c>Module.FilesEncoding</c>: git's i18n.filesEncoding, else UTF-8); git is asked when it is not given.</param>
    public EditorViewModel(IDialogHost host, string path, bool showWarning,
        Func<string, Encoding>? filesEncoding = null)
        : base(host)
    {
        Path = System.IO.Path.GetFullPath(path);
        ShowWarning = showWarning;
        _filesEncoding = filesEncoding ?? (folder => GitModules.Open(folder).FilesEncoding);
    }

    /// <summary>
    ///  The file, also the window's title.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///  Upstream's <c>panelMessage</c>: "Here be dragons!", for .git/config.
    /// </summary>
    public bool ShowWarning { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string? Text { get; set; }

    /// <summary>
    ///  True until the file is read, so nothing is typed into a text that is replaced.
    /// </summary>
    [ObservableProperty]
    public partial bool IsReadOnly { get; private set; } = true;

    [ObservableProperty]
    public partial string? ErrorText { get; set; }

    /// <summary>
    ///  Upstream's question about unsaved changes, asked when the window is closed with them.
    /// </summary>
    [ObservableProperty]
    public partial bool AsksToSave { get; set; }

    /// <summary>
    ///  True when the window closed with the file saved or left unchanged; false when the changes were discarded or the
    ///  file could not be read. The app exits with upstream's code for it (0 or -1), which tells git whether to go on.
    /// </summary>
    public bool Accepted { get; private set; }

    /// <summary>
    ///  Completes when the file has been read.
    /// </summary>
    public Task Loaded => _loaded.Task;

    public bool HasChanges => _file is not null && (Text ?? "") != _savedText;

    private bool CanSave => HasChanges;

    /// <summary>
    ///  Reads the file in its repository's files encoding; a file that does not exist yet opens empty and is created on
    ///  save, so the verb also works for a new file. The window calls it when it opens.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            Encoding encoding = await Task.Run(() =>
                System.IO.Path.GetDirectoryName(Path) is { } folder && Directory.Exists(folder)
                ? _filesEncoding(folder)
                : new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            _file = File.Exists(Path)
                ? await EditorFile.ReadAsync(Path, encoding)
                : new EditorFile("", "\n", HasByteOrderMark: false, encoding);
            _savedText = _file.Text;
            Text = _file.Text;
            IsReadOnly = false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            _loaded.TrySetResult();
        }
    }

    /// <summary>
    ///  Upstream's <c>FormClosing</c>: true when the close is cancelled, because changes are not saved and the question
    ///  is asked. A close without changes accepts the file when it was read.
    /// </summary>
    public bool OnClosing()
    {
        if (_closeDecided)
        {
            return false;
        }

        if (HasChanges)
        {
            AsksToSave = true;
            return true;
        }

        Accepted = _file is not null;
        return false;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => SaveFileAsync();

    [RelayCommand]
    private async Task SaveAndCloseAsync()
    {
        if (await SaveFileAsync())
        {
            CloseWith(accepted: true);
        }
    }

    [RelayCommand]
    private void Discard() => CloseWith(accepted: false);

    [RelayCommand]
    private void KeepEditing() => AsksToSave = false;

    // An error shows next to Save, as the window's toolbar showed it.
    private async Task<bool> SaveFileAsync()
    {
        if (_file is null)
        {
            return false;
        }

        try
        {
            string text = Text ?? "";
            await _file.WriteAsync(Path, text);
            _savedText = text;
            SaveCommand.NotifyCanExecuteChanged();
            ErrorText = "";
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorText = ex.Message;
            return false;
        }
    }

    private void CloseWith(bool accepted)
    {
        _closeDecided = true;
        Accepted = accepted;
        Host.Close(null);
    }
}
