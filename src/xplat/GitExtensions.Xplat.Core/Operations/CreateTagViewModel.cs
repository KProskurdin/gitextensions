using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands.Git;
using GitCommands.Git.Tag;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormCreateTag</c> (rules in <see cref="CreateTagDialog"/>): a lightweight, annotated or
///  signed tag on a commit, pushed to the current remote when asked. Closes its window with true when the tag was created.
/// </summary>
public sealed partial class CreateTagViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormCreateTag";

    private const string ErrorCaption = "Error";

    private readonly IGitModule _module;
    private readonly string? _revision;
    private readonly Func<string, Task<bool>> _runGitWithOutput;
    private readonly Func<string, string, Task<bool>> _pushTag;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string _currentRemote = "";

    /// <param name="revision">The commit to tag, or null (or an artificial commit) for the current checkout.</param>
    /// <param name="runGitWithOutput">Runs the tag command with its output shown (upstream's process dialog).</param>
    /// <param name="pushTag">Pushes a tag to a remote (remote, tag), with upstream's push scripts around it.</param>
    public CreateTagViewModel(IDialogHost host, IGitModule module, string? revision, Func<string, Task<bool>> runGitWithOutput,
        Func<string, string, Task<bool>> pushTag)
        : base(host)
    {
        _module = module;
        _revision = revision;
        _runGitWithOutput = runGitWithOutput;
        _pushTag = pushTag;
        Operations =
        [
            .. CreateTagDialog.Operations.Select(operation =>
                host.Translate(Category, TranslationName(operation.Operation), operation.Text)),
        ];
        TagName = "";
        KeyId = "";
        Message = "";
    }

    /// <summary>
    ///  The kinds of tag in upstream's drop-down, translated.
    /// </summary>
    public IReadOnlyList<string> Operations { get; }

    /// <summary>
    ///  The chosen kind's place in <see cref="Operations"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesKeyId))]
    [NotifyPropertyChangedFor(nameof(UsesMessage))]
    public partial int SelectedOperationIndex { get; set; }

    [ObservableProperty]
    public partial string TagName { get; set; }

    /// <summary>
    ///  The commit to tag; null when none is chosen.
    /// </summary>
    [ObservableProperty]
    public partial ObjectId? Revision { get; set; }

    [ObservableProperty]
    public partial string KeyId { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; }

    /// <summary>
    ///  Upstream's "Push tag to '...'".
    /// </summary>
    [ObservableProperty]
    public partial bool PushTag { get; set; }

    /// <summary>
    ///  The push check box's text with the remote's name, translated; null until loaded.
    /// </summary>
    [ObservableProperty]
    public partial string? PushTagCaption { get; private set; }

    /// <summary>
    ///  Upstream's <c>AnnotateDropDownChanged</c>: the key id is for a specific GPG key only.
    /// </summary>
    public bool UsesKeyId => CreateTagDialog.UsesKeyId(SelectedOperation);

    /// <summary>
    ///  Upstream's <c>AnnotateDropDownChanged</c>: every kind but a lightweight tag takes a message.
    /// </summary>
    public bool UsesMessage => CreateTagDialog.UsesMessage(SelectedOperation);

    /// <summary>
    ///  The repository the commit picker resolves commits in.
    /// </summary>
    public IGitModule Module => _module;

    /// <summary>
    ///  Completes when the commit and the remote are shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    private TagOperation SelectedOperation => CreateTagDialog.Operations[Math.Max(SelectedOperationIndex, 0)].Operation;

    /// <summary>
    ///  Upstream's constructor and <c>FormCreateTag_Load</c>: the given commit, else the current checkout (an artificial
    ///  commit counts as none), and the remote in the push check box. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        try
        {
            (ObjectId? commit, string remote) = await Task.Run(() =>
            {
                string? given = _revision is null || ArtificialCommits.IsArtificial(_revision) ? null : _revision;
                ObjectId id = given is null ? _module.GetCurrentCheckout() : _module.RevParse(given);
                return (id.IsZero ? (ObjectId?)null : id, CreateTagDialog.PushRemote(_module));
            });
            _currentRemote = remote;
            PushTagCaption = string.Format(Host.Translate(Category, "_pushToCaption", CreateTagDialog.PushToCaption), remote);
            Revision = commit;
        }
        finally
        {
            _loaded.TrySetResult();
        }
    });

    // Upstream's OkClick: the tag, then the push when asked; upstream's argument errors are shown and the window stays.
    [RelayCommand]
    private Task CreateAsync() => GuardAsync(async () =>
    {
        try
        {
            if (await CreateTagAsync() is not { } tagName)
            {
                return;
            }

            if (PushTag)
            {
                await _pushTag(_currentRemote, tagName);
            }
        }
        catch (ArgumentException ex)
        {
            await Host.ShowMessageAsync(ex.Message, Host.Translate(ErrorCaption));
        }
    });

    [RelayCommand]
    private void Cancel() => Host.Close(false);

    // Upstream's CreateTag: a commit is needed; the window closes once git succeeded.
    private async Task<string?> CreateTagAsync()
    {
        if (Revision is not { } objectId)
        {
            await Host.ShowMessageAsync(
                Host.Translate(Category, "_noRevisionSelected", CreateTagDialog.NoRevisionSelected),
                Host.Translate(Category, "_messageCaption", CreateTagDialog.MessageCaption));
            return null;
        }

        string tagName = TagName;
        GitCreateTagArgs args = new(tagName, objectId, SelectedOperation, Message, KeyId);
        if (!await CreateTagDialog.CreateAsync(_module, args, _runGitWithOutput))
        {
            return null;
        }

        Host.Close(true);
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
}
