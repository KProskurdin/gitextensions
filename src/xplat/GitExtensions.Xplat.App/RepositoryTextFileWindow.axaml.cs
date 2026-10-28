using Avalonia.Controls;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormGitAttributes</c> and <c>FormMailMap</c> (the file in <see cref="RepositoryTextFile"/>): the file's
///  text to edit beside upstream's help. Save writes and closes; closing with changes asks upstream's question. Closes with
///  true when the file was saved.
/// </summary>
public partial class RepositoryTextFileWindow : Window
{
    private readonly string _repositoryPath;
    private readonly RepositoryTextFile _file;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string _savedText = "";
    private bool _saved;
    private bool _closeDecided;

    public RepositoryTextFileWindow()
        : this("", RepositoryTextFiles.GitAttributes)
    {
    }

    public RepositoryTextFileWindow(string repositoryPath, RepositoryTextFile file)
    {
        _repositoryPath = repositoryPath;
        _file = file;
        InitializeComponent();
        Title = file.Title;
        label1.Text = file.Help;
        UpstreamTranslation.Apply(this, file.Form);
        InitialFocus.Attach(this, () => FileText);
        Save.Click += (_, _) => Run(async () =>
        {
            if (await SaveAsync())
            {
                Close(true);
            }
        });
        Closing += OnClosing;
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  Completes when the file's text is shown, for tests.
    /// </summary>
    public Task FileLoaded => _loaded.Task;

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));

    private async Task LoadAsync()
    {
        _savedText = await RepositoryTextFiles.ReadAsync(_repositoryPath, _file);
        FileText.Text = _savedText;
        _loaded.TrySetResult();
    }

    // Upstream's SaveFile: upstream's error when the file cannot be written.
    private async Task<bool> SaveAsync()
    {
        try
        {
            string text = FileText.Text ?? "";
            await RepositoryTextFiles.WriteAsync(_repositoryPath, _file, text);
            _savedText = text;
            _saved = true;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await new MessageBoxWindow(
                string.Format(UpstreamTranslation.ByText(RepositoryTextFiles.CannotSave), _file.FileName)
                + Environment.NewLine + UpstreamTranslation.ByText(RepositoryTextFiles.CheckAccessible)
                + Environment.NewLine + ex.Message,
                string.Format(UpstreamTranslation.ByText(RepositoryTextFiles.CannotSaveCaption), _file.FileName),
                MessageBoxButtons.OK).ShowDialog(this);
            return false;
        }
    }

    // Upstream's FormClosing: changes not saved are saved, dropped or kept open after upstream's question.
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeDecided || (FileText.Text ?? "") == _savedText)
        {
            _closeDecided = true;
            return;
        }

        e.Cancel = true;
        Run(async () =>
        {
            MessageBoxWindow question = new(
                string.Format(UpstreamTranslation.ByText(RepositoryTextFiles.SaveQuestion), _file.FileName),
                UpstreamTranslation.ByText(RepositoryTextFiles.SaveQuestionCaption), MessageBoxButtons.YesNoCancel);
            await question.ShowDialog(this);
            if (question.Result == DialogResult.No || (question.Result == DialogResult.Yes && await SaveAsync()))
            {
                _closeDecided = true;
                Close(_saved);
            }
        });
    }
}
