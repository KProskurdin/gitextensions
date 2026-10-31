using Avalonia.Controls;
using GitCommands;
using GitCommands.Git;
using GitCommands.Git.Tag;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormCreateTag</c> (rules in <see cref="CreateTagDialog"/>): a lightweight, annotated or signed tag on a
///  commit, pushed to the current remote when asked. Closes with true when the tag was created.
/// </summary>
public partial class CreateTagWindow : Window
{
    private readonly GitModule _module;
    private readonly string? _revision;
    private readonly Func<string, Task<bool>> _runGitWithOutput;
    private readonly Func<string, string, Task<bool>> _pushTag;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string _currentRemote = "";

    public CreateTagWindow()
        : this("", null, _ => Task.FromResult(false), (_, _) => Task.FromResult(false))
    {
    }

    /// <param name="repositoryPath">The repository the tag is created in.</param>
    /// <param name="revision">The commit to tag, or null (or an artificial commit) for the current checkout.</param>
    /// <param name="runGitWithOutput">Runs the tag command with its output shown (upstream's process dialog).</param>
    /// <param name="pushTag">Pushes a tag to a remote (remote, tag), with upstream's push scripts around it.</param>
    public CreateTagWindow(string repositoryPath, string? revision, Func<string, Task<bool>> runGitWithOutput,
        Func<string, string, Task<bool>> pushTag)
    {
        _module = GitModules.Open(repositoryPath);
        _revision = revision;
        _runGitWithOutput = runGitWithOutput;
        _pushTag = pushTag;
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormCreateTag");
        InitialFocus.Attach(this, () => textBoxTagName);
        commitPickerSmallControl1.Module = _module;
        annotate.ItemsSource = CreateTagDialog.Operations
            .Select(operation => UpstreamTranslation.Text("FormCreateTag", TranslationName(operation.Operation), operation.Text))
            .ToList();
        annotate.SelectionChanged += (_, _) => ShowOperation();
        annotate.SelectedIndex = 0;
        Ok.Click += (_, _) => Run(OkAsync);
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  Completes when the commit and the remote are shown, for tests.
    /// </summary>
    public Task TagLoaded => _loaded.Task;

    private TagOperation SelectedOperation => CreateTagDialog.Operations[Math.Max(annotate.SelectedIndex, 0)].Operation;

    // Upstream's constructor and FormCreateTag_Load: the given commit, else the current checkout (an artificial commit
    // counts as none), and the remote in the push check box.
    private async Task LoadAsync()
    {
        try
        {
            (string? commit, string remote) = await Task.Run(() =>
            {
                string? given = _revision is null || ArtificialCommits.IsArtificial(_revision) ? null : _revision;
                string? current = given ?? (_module.GetCurrentCheckout() is { IsZero: false } head ? head.ToString() : null);
                return (current, CreateTagDialog.PushRemote(_module));
            });
            _currentRemote = remote;
            pushTag.Content = string.Format(UpstreamTranslation.Text("FormCreateTag", "_pushToCaption", CreateTagDialog.PushToCaption),
                remote);
            if (commit is not null)
            {
                await commitPickerSmallControl1.SetCommitAsync(commit);
            }
        }
        finally
        {
            _loaded.TrySetResult();
        }
    }

    // Upstream's AnnotateDropDownChanged.
    private void ShowOperation()
    {
        TagOperation operation = SelectedOperation;
        textBoxGpgKey.IsEnabled = keyIdLbl.IsEnabled = CreateTagDialog.UsesKeyId(operation);
        tagMessage.IsEnabled = CreateTagDialog.UsesMessage(operation);
    }

    // Upstream's OkClick: the tag, then the push when asked; an error is shown and the window stays.
    private async Task OkAsync()
    {
        try
        {
            if (await CreateTagAsync() is not { } tagName)
            {
                return;
            }

            if (pushTag.IsChecked == true)
            {
                await _pushTag(_currentRemote, tagName);
            }
        }
        catch (ArgumentException ex)
        {
            await ShowMessageAsync(ex.Message, UpstreamTranslation.ByText("Error"));
        }
    }

    // Upstream's CreateTag: a commit is needed; the window closes once git succeeded.
    private async Task<string?> CreateTagAsync()
    {
        if (commitPickerSmallControl1.Selected is not { } objectId)
        {
            await ShowMessageAsync(
                UpstreamTranslation.Text("FormCreateTag", "_noRevisionSelected", CreateTagDialog.NoRevisionSelected),
                UpstreamTranslation.Text("FormCreateTag", "_messageCaption", CreateTagDialog.MessageCaption));
            return null;
        }

        string tagName = textBoxTagName.Text ?? "";
        GitCreateTagArgs args = new(tagName, objectId, SelectedOperation, tagMessage.Text ?? "", textBoxGpgKey.Text ?? "");
        if (!await CreateTagDialog.CreateAsync(_module, args, _runGitWithOutput))
        {
            return null;
        }

        Close(true);
        return tagName;
    }

    private static string TranslationName(TagOperation operation)
        => operation switch
        {
            TagOperation.Lightweight => "_trsLightweight",
            TagOperation.Annotate => "_trsAnnotated",
            TagOperation.SignWithDefaultKey => "_trsSignDefault",
            _ => "_trsSignSpecificKey",
        };

    private Task ShowMessageAsync(string text, string caption)
        => new MessageBoxWindow(text, caption, MessageBoxButtons.OK).ShowDialog(this);

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}
