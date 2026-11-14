using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The view model of upstream's <c>FormGitAttributes</c> and <c>FormMailMap</c> (the file in
///  <see cref="RepositoryTextFile"/>): the file's text to edit beside upstream's help. Save writes and closes the window
///  with true; closing it with changes asks upstream's question first (<see cref="MayClose"/>, <see cref="AskToCloseAsync"/>).
/// </summary>
public sealed partial class RepositoryTextFileViewModel : DialogViewModel
{
    // Upstream's names of the window's title and its help label, for their translations.
    private const string TitleName = "$this";
    private const string HelpName = "label1";

    private readonly string _repositoryPath;
    private readonly RepositoryTextFile _file;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string _savedText = "";
    private bool _saved;
    private bool _closeDecided;

    public RepositoryTextFileViewModel(IDialogHost host, string repositoryPath, RepositoryTextFile file)
        : base(host)
    {
        _repositoryPath = repositoryPath;
        _file = file;
        Title = Host.Translate(file.Form, TitleName, file.Title);
        Help = Host.Translate(file.Form, HelpName, file.Help);
    }

    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public string Category => _file.Form;

    public string Title { get; }

    public string Help { get; }

    [ObservableProperty]
    public partial string Text { get; set; } = "";

    /// <summary>
    ///  Completes when the file's text is shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  True when the window may close without a question: nothing changed since the last read or save, or the question
    ///  was answered.
    /// </summary>
    public bool MayClose => _closeDecided || Text == _savedText;

    /// <summary>
    ///  Reads the file. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        _savedText = await RepositoryTextFiles.ReadAsync(_repositoryPath, _file);
        Text = _savedText;
        _loaded.TrySetResult();
    });

    /// <summary>
    ///  Upstream's <c>FormClosing</c>: changes not saved are saved, dropped or kept after upstream's question; the window is
    ///  closed (with true when the file was saved) unless the user cancelled or the save failed.
    /// </summary>
    public Task AskToCloseAsync() => GuardAsync(async () =>
    {
        bool? save = await Host.AskAsync(string.Format(Host.Translate(RepositoryTextFiles.SaveQuestion), _file.FileName),
            Host.Translate(RepositoryTextFiles.SaveQuestionCaption));
        if (save == false || (save == true && await SaveFileAsync()))
        {
            _closeDecided = true;
            Host.Close(_saved);
        }
    });

    [RelayCommand]
    private Task SaveAsync() => GuardAsync(async () =>
    {
        if (await SaveFileAsync())
        {
            Host.Close(true);
        }
    });

    // Upstream's SaveFile: upstream's error when the file cannot be written.
    private async Task<bool> SaveFileAsync()
    {
        try
        {
            string text = Text;
            await RepositoryTextFiles.WriteAsync(_repositoryPath, _file, text);
            _savedText = text;
            _saved = true;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Host.ShowMessageAsync(
                string.Format(Host.Translate(RepositoryTextFiles.CannotSave), _file.FileName)
                + Environment.NewLine + Host.Translate(RepositoryTextFiles.CheckAccessible)
                + Environment.NewLine + ex.Message,
                string.Format(Host.Translate(RepositoryTextFiles.CannotSaveCaption), _file.FileName));
            return false;
        }
    }
}
