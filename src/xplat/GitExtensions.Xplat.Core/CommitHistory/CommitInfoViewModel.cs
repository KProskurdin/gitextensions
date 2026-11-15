using System.Globalization;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  One of upstream commit info's menu check items.
/// </summary>
public enum CommitInfoOption
{
    BranchesLocal,
    BranchesRemote,
    BranchesRemoteIfNoLocal,
    Tags,
    AnnotatedTagsMessages,
    DerivesFromTag,
}

/// <summary>
///  Upstream's <c>CommitInfo</c> and <c>CommitInfoHeader</c> without a control (PLAN.md principle 6): a commit's header,
///  message and the information read after it (annotated tags, related links, the branches and tags containing it, the tag
///  it derives from), the avatar, and upstream's menu. The view lays the header out, finds the link under the pointer and
///  opens addresses.
/// </summary>
public sealed partial class CommitInfoViewModel : ObservableObject
{
    // Upstream's CommitInfo text.
    public const string CopyLinkText = "Copy &link ({0})";

    private readonly Func<CommitInfoRequest, CancellationToken, Task<IReadOnlyList<IReadOnlyList<InfoSpan>>>> _readRevisionInfo;
    private readonly Func<string, string> _translate;
    private CancellationTokenSource? _revisionInfoLoad;
    private bool _showAllBranches;
    private bool _showAllTags;

    /// <param name="readRevisionInfo">Reads what shows after the message; upstream's reader with
    ///  <paramref name="translate"/> without one.</param>
    /// <param name="translate">Upstream's translation of an English text.</param>
    public CommitInfoViewModel(IAppPreferences preferences,
        Func<CommitInfoRequest, CancellationToken, Task<IReadOnlyList<IReadOnlyList<InfoSpan>>>>? readRevisionInfo = null,
        Func<string, string>? translate = null)
    {
        Preferences = preferences;
        _translate = translate ?? (text => text);
        CommitInfoReader reader = new(_translate);
        _readRevisionInfo = readRevisionInfo ?? reader.LoadAsync;
    }

    /// <summary>
    ///  Upstream's <c>CommandClicked</c>: a link to a commit, branch or tag (gotocommit, gotobranch, gototag) with the
    ///  revision it names. Without a handler, as upstream's, such links do nothing.
    /// </summary>
    public event EventHandler<string>? RevisionLinkClicked;

    /// <summary>
    ///  Upstream's "Add notes" item of the commit info's menu; the window edits the shown commit's notes.
    /// </summary>
    public event EventHandler? AddNotesRequested;

    public IAppPreferences Preferences { get; set; }

    /// <summary>
    ///  The repository of the commit shown; the information after the message is read from it.
    /// </summary>
    public string? RepositoryPath { get; set; }

    /// <summary>
    ///  Puts a text on the clipboard. Set by the view.
    /// </summary>
    public Func<string, Task>? SetClipboard { get; set; }

    /// <summary>
    ///  Opens an address in the browser. Set by the view.
    /// </summary>
    public Func<Uri, Task>? OpenUri { get; set; }

    /// <summary>
    ///  The commit shown, or null.
    /// </summary>
    [ObservableProperty]
    public partial CommitDetails? Details { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<InfoHeaderRow> HeaderRows { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<IReadOnlyList<InfoSpan>> MessageLines { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<IReadOnlyList<InfoSpan>> RevisionInfoLines { get; private set; } = [];

    /// <summary>
    ///  Upstream's <c>ShowAuthorAvatarInCommitInfo</c>, for a commit.
    /// </summary>
    [ObservableProperty]
    public partial bool IsAvatarVisible { get; private set; }

    [ObservableProperty]
    public partial string? AvatarName { get; private set; }

    [ObservableProperty]
    public partial string? AvatarEmail { get; private set; }

    /// <summary>
    ///  The link the menu was opened on, which it offers to copy; set by the view before the menu opens.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCopyLinkVisible), nameof(CopyLinkHeader))]
    public partial string? MenuLink { get; set; }

    public bool IsCopyLinkVisible => MenuLink is not null;

    public string CopyLinkHeader
        => string.Format(CultureInfo.CurrentCulture, _translate(CopyLinkText), MenuLink);

    public bool BranchesLocal => Preferences.CommitInfoOptions.BranchesLocal;

    public bool BranchesRemote => Preferences.CommitInfoOptions.BranchesRemote;

    public bool BranchesRemoteIfNoLocal => Preferences.CommitInfoOptions.BranchesRemoteIfNoLocal;

    public bool Tags => Preferences.CommitInfoOptions.Tags;

    public bool AnnotatedTagsMessages => Preferences.CommitInfoOptions.AnnotatedTagsMessages;

    public bool DerivesFromTag => Preferences.CommitInfoOptions.DerivesFromTag;

    /// <summary>
    ///  Upstream's branch sort (RefsSortBy, RefsSortOrder, done by git) and priorities, for the branches containing the
    ///  commit.
    /// </summary>
    public static RefSorting UserRefSorting(IAppPreferences preferences)
        => new(preferences.RefsSortBy, preferences.RefsSortOrder, preferences.PrioritizedBranchNames,
            preferences.PrioritizedRemoteNames);

    /// <summary>
    ///  Shows <paramref name="details"/>, or <paramref name="error"/> when there are none; the header lists
    ///  <paramref name="childHashes"/> as the commit's children (upstream lists the children its grid loaded).
    /// </summary>
    public void Show(CommitDetails? details, string? error, IReadOnlyList<string> childHashes)
    {
        Details = details;
        ShowAvatar();
        _showAllBranches = false;
        _showAllTags = false;
        if (details is null)
        {
            HeaderRows = [];
            MessageLines = string.IsNullOrEmpty(error) ? [] : [[new InfoSpan(error)]];
            RevisionInfoLines = [];
            return;
        }

        if (details.Data is { } data)
        {
            data.ChildIds = [.. childHashes.Select(ObjectId.Parse)];
            HeaderRows = CommitInfoText.Header(data);
        }
        else
        {
            HeaderRows = [];
        }

        MessageLines = details.MessageLines ?? CommitInfoText.Parse(WebUtility.HtmlEncode(details.Message));
        Run(LoadRevisionInfoAsync);
    }

    /// <summary>
    ///  Upstream's ShowAuthorAvatarInCommitInfo, applied again when the settings change. Details' author is
    ///  "name &lt;email&gt;".
    /// </summary>
    public void ShowAvatar()
    {
        string? author = Details?.Author;
        IsAvatarVisible = author is not null && Preferences.ShowAuthorAvatarInCommitInfo;
        if (author is null)
        {
            return;
        }

        int open = author.LastIndexOf('<');
        int close = author.LastIndexOf('>');
        AvatarName = open > 0 ? author[..open].Trim() : author;
        AvatarEmail = open >= 0 && close > open ? author[(open + 1)..close] : "";
    }

    /// <summary>
    ///  Reads the menu's checks again, as the menu opens.
    /// </summary>
    public void RefreshMenu()
    {
        foreach (string name in (string[])[nameof(BranchesLocal), nameof(BranchesRemote), nameof(BranchesRemoteIfNoLocal),
                     nameof(Tags), nameof(AnnotatedTagsMessages), nameof(DerivesFromTag)])
        {
            OnPropertyChanged(name);
        }
    }

    /// <summary>
    ///  Upstream's <c>LinkFactory.ExecuteLink</c>: links to revisions go to <see cref="RevisionLinkClicked"/>, "show all"
    ///  lists them all, other links open in the browser.
    /// </summary>
    [RelayCommand]
    private void OpenLink(string link)
    {
        if (!CommitInfoText.TryParseCommand(link, out string command, out string data))
        {
            if (Uri.TryCreate(link, UriKind.Absolute, out Uri? address) && OpenUri is { } open)
            {
                Run(() => open(address));
            }

            return;
        }

        switch (command)
        {
            case "gotocommit":
            case "gotobranch":
            case "gototag":
                RevisionLinkClicked?.Invoke(this, data);
                break;
            case "showall":
                _showAllBranches |= data == "branches";
                _showAllTags |= data == "tags";
                Run(LoadRevisionInfoAsync);
                break;
        }
    }

    [RelayCommand]
    private Task CopyLinkAsync() => CopyTextAsync(MenuLink ?? "");

    // Upstream's "Copy commit info": the header without relative dates, children and parents, then the message.
    [RelayCommand]
    private Task CopyCommitInfoAsync()
        => CopyTextAsync($"{CommitInfoText.HeaderPlainText(HeaderRows)}{Environment.NewLine}{Environment.NewLine}"
                         + string.Join("\n", MessageLines.Select(CommitInfoText.PlainText)).Replace("\n", Environment.NewLine));

    [RelayCommand]
    private void AddNotes() => AddNotesRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void ToggleOption(CommitInfoOption option)
    {
        CommitInfoOptions options = Preferences.CommitInfoOptions;
        Preferences.CommitInfoOptions = option switch
        {
            CommitInfoOption.BranchesLocal => options with { BranchesLocal = !options.BranchesLocal },
            CommitInfoOption.BranchesRemote => options with { BranchesRemote = !options.BranchesRemote },
            CommitInfoOption.BranchesRemoteIfNoLocal => options with { BranchesRemoteIfNoLocal = !options.BranchesRemoteIfNoLocal },
            CommitInfoOption.Tags => options with { Tags = !options.Tags },
            CommitInfoOption.AnnotatedTagsMessages => options with { AnnotatedTagsMessages = !options.AnnotatedTagsMessages },
            CommitInfoOption.DerivesFromTag => options with { DerivesFromTag = !options.DerivesFromTag },
            _ => options,
        };
        Preferences.Save();
        RefreshMenu();
        Run(LoadRevisionInfoAsync);
    }

    private Task CopyTextAsync(string text) => SetClipboard is { } setClipboard ? setClipboard(text) : Task.CompletedTask;

    private async Task LoadRevisionInfoAsync()
    {
        if (_revisionInfoLoad is { } previous)
        {
            await previous.CancelAsync();
        }

        CancellationTokenSource load = _revisionInfoLoad = new CancellationTokenSource();
        RevisionInfoLines = [];
        if (Details is not { } details || RepositoryPath is not { } path)
        {
            return;
        }

        CommitInfoRequest request = new(path, details.Hash, Preferences.CommitInfoOptions, UserRefSorting(Preferences),
            details.Links ?? [], _showAllBranches, _showAllTags);
        IReadOnlyList<IReadOnlyList<InfoSpan>> lines;
        try
        {
            lines = await _readRevisionInfo(request, load.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (load == _revisionInfoLoad)
        {
            RevisionInfoLines = lines;
        }
    }

    // An error shows where the information after the message shows, as upstream's control did.
    private void Run(Func<Task> action) => _ = GuardAsync(action);

    private async Task GuardAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // Nothing to report.
        }
        catch (Exception ex)
        {
            RevisionInfoLines = [[new InfoSpan(ex.Message)]];
        }
    }
}
