using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>CommitInfo</c> and <c>CommitInfoHeader</c>: a commit's header, message and the information read after it
///  (annotated tags, related links, the branches and tags containing it, the tag it derives from), with upstream's menu.
///  Shared by the browse window and upstream's <c>CommitDiff</c> (<see cref="CommitDiffView"/>).
/// </summary>
public partial class CommitInfoView : UserControl
{
    // Upstream's CommitInfo text.
    private const string CopyLinkText = "Copy &link ({0})";

    private CancellationTokenSource? _revisionInfoLoad;
    private IReadOnlyList<InfoHeaderRow> _headerRows = [];
    private bool _showAllBranches;
    private bool _showAllTags;
    private string? _commitInfoLink;

    public CommitInfoView()
    {
        InitializeComponent();
        DetailMessage.LinkClicked += (_, link) => OpenInfoLink(link);
        DetailRevisionInfo.LinkClicked += (_, link) => OpenInfoLink(link);

        // The link under the pointer, read before the menu opens (it opens while the request bubbles).
        RevisionInfoScroll.AddHandler(ContextRequestedEvent, (_, e) => _commitInfoLink = LinkAt(e),
            RoutingStrategies.Tunnel);
        CommitInfoContextMenu.Opening += (_, _) => ShowCommitInfoMenu();
        CopyCommitInfoLinkMenuItem.Click += (_, _) => Run(() => CopyTextAsync(_commitInfoLink ?? ""));
        CopyCommitInfoMenuItem.Click += (_, _) => Run(() => CopyTextAsync(PlainText()));
        ShowContainedInBranchesMenuItem.Click += (_, _) => SetOptions(options => options with
        {
            BranchesLocal = !options.BranchesLocal
        });
        ShowContainedInBranchesRemoteMenuItem.Click += (_, _) => SetOptions(options => options with
        {
            BranchesRemote = !options.BranchesRemote
        });
        ShowContainedInBranchesRemoteIfNoLocalMenuItem.Click += (_, _) => SetOptions(options => options with
        {
            BranchesRemoteIfNoLocal = !options.BranchesRemoteIfNoLocal
        });
        ShowContainedInTagsMenuItem.Click += (_, _) => SetOptions(options => options with
        {
            Tags = !options.Tags
        });
        ShowAnnotatedTagsMessagesMenuItem.Click += (_, _) => SetOptions(options => options with
        {
            AnnotatedTagsMessages = !options.AnnotatedTagsMessages
        });
        ShowDerivesFromTagMenuItem.Click += (_, _) => SetOptions(options => options with
        {
            DerivesFromTag = !options.DerivesFromTag
        });
    }

    /// <summary>
    ///  Upstream's <c>CommandClicked</c>: a link to a commit, branch or tag (gotocommit, gotobranch, gototag) with the
    ///  revision it names. Without a handler, as upstream's, such links do nothing.
    /// </summary>
    public event EventHandler<string>? RevisionLinkClicked;

    public IAppPreferences Preferences { get; set; } = AppServices.Preferences;

    /// <summary>
    ///  The repository of the commit shown; the information after the message is read from it.
    /// </summary>
    public string? RepositoryPath { get; set; }

    /// <summary>
    ///  The commit shown, or null.
    /// </summary>
    public CommitDetails? Details { get; private set; }

    /// <summary>
    ///  Upstream's branch sort (RefsSortBy, RefsSortOrder, done by git) and priorities, for the branches containing the
    ///  commit.
    /// </summary>
    internal static RefSorting UserRefSorting(IAppPreferences preferences)
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
            ShowHeader([]);
            DetailMessage.Lines = string.IsNullOrEmpty(error) ? [] : [[new InfoSpan(error)]];
            DetailRevisionInfo.Lines = [];
            return;
        }

        if (details.Data is { } data)
        {
            data.ChildIds = [.. childHashes.Select(ObjectId.Parse)];
            ShowHeader(CommitInfoText.Header(data));
        }
        else
        {
            ShowHeader([]);
        }

        DetailMessage.Lines = details.MessageLines ?? CommitInfoText.Parse(WebUtility.HtmlEncode(details.Message));
        Run(LoadRevisionInfoAsync);
    }

    /// <summary>
    ///  Upstream's ShowAuthorAvatarInCommitInfo, applied again when the settings change.
    /// </summary>
    public void ShowAvatar()
    {
        // CommitDetails.Author is "name <email>".
        string? author = Details?.Author;
        DetailAvatar.IsVisible = author is not null && Preferences.ShowAuthorAvatarInCommitInfo;
        if (author is null)
        {
            return;
        }

        int open = author.LastIndexOf('<');
        int close = author.LastIndexOf('>');
        DetailAvatar.AuthorName = open > 0 ? author[..open].Trim() : author;
        DetailAvatar.Email = open >= 0 && close > open ? author[(open + 1)..close] : "";
    }

    // Upstream's CommitInfoHeader: the labels in a column as wide as the longest, their values beside them.
    private void ShowHeader(IReadOnlyList<InfoHeaderRow> rows)
    {
        _headerRows = rows;
        DetailHeader.Children.Clear();
        DetailHeader.RowDefinitions.Clear();
        for (int i = 0; i < rows.Count; i++)
        {
            DetailHeader.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            TextBlock label = new() { Text = rows[i].Label, Margin = new Thickness(0, 0, 12, 0) };
            InfoTextBlock value = new() { Lines = [rows[i].Value], TextWrapping = TextWrapping.Wrap };
            value.LinkClicked += (_, link) => OpenInfoLink(link);
            Grid.SetRow(label, i);
            Grid.SetRow(value, i);
            Grid.SetColumn(value, 1);
            DetailHeader.Children.Add(label);
            DetailHeader.Children.Add(value);
        }
    }

    private async Task LoadRevisionInfoAsync()
    {
        if (_revisionInfoLoad is { } previous)
        {
            await previous.CancelAsync();
        }

        CancellationTokenSource load = _revisionInfoLoad = new CancellationTokenSource();
        DetailRevisionInfo.Lines = [];
        if (Details is not { } details || RepositoryPath is not { } path)
        {
            return;
        }

        CommitInfoRequest request = new(path, details.Hash, Preferences.CommitInfoOptions, UserRefSorting(Preferences),
            details.Links ?? [], _showAllBranches, _showAllTags);
        IReadOnlyList<IReadOnlyList<InfoSpan>> lines;
        try
        {
            lines = await new CommitInfoReader().LoadAsync(request, load.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (load == _revisionInfoLoad)
        {
            DetailRevisionInfo.Lines = lines;
        }
    }

    // Upstream's LinkFactory.ExecuteLink: links to revisions go to the handler, "show all" lists them all, other links
    // open in the browser.
    private void OpenInfoLink(string link)
    {
        if (!CommitInfoText.TryParseCommand(link, out string command, out string data))
        {
            if (Uri.TryCreate(link, UriKind.Absolute, out Uri? address)
                && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
            {
                Run(() => launcher.LaunchUriAsync(address));
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

    private string? LinkAt(ContextRequestedEventArgs e)
    {
        InfoTextBlock? block = (e.Source as Visual)?.FindAncestorOfType<InfoTextBlock>(includeSelf: true);
        return block is not null && e.TryGetPosition(block, out Point point) ? block.LinkAt(point) : null;
    }

    private void ShowCommitInfoMenu()
    {
        CopyCommitInfoLinkMenuItem.IsVisible = _commitInfoLink is not null;
        CopyCommitInfoLinkMenuItem.Header = string.Format(UpstreamTranslation.ByText(CopyLinkText), _commitInfoLink);
        CommitInfoOptions options = Preferences.CommitInfoOptions;
        ShowContainedInBranchesMenuItem.IsChecked = options.BranchesLocal;
        ShowContainedInBranchesRemoteMenuItem.IsChecked = options.BranchesRemote;
        ShowContainedInBranchesRemoteIfNoLocalMenuItem.IsChecked = options.BranchesRemoteIfNoLocal;
        ShowContainedInTagsMenuItem.IsChecked = options.Tags;
        ShowAnnotatedTagsMessagesMenuItem.IsChecked = options.AnnotatedTagsMessages;
        ShowDerivesFromTagMenuItem.IsChecked = options.DerivesFromTag;
    }

    private void SetOptions(Func<CommitInfoOptions, CommitInfoOptions> change)
    {
        Preferences.CommitInfoOptions = change(Preferences.CommitInfoOptions);
        Preferences.Save();
        Run(LoadRevisionInfoAsync);
    }

    // Upstream's "Copy commit info": the header without relative dates, children and parents, then the message.
    private string PlainText()
        => $"{CommitInfoText.HeaderPlainText(_headerRows)}{Environment.NewLine}{Environment.NewLine}"
           + DetailMessage.PlainText.Replace("\n", Environment.NewLine);

    private async Task CopyTextAsync(string text)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    private void Run(Func<Task> action) => UiActions.Run(action, ex => DetailRevisionInfo.Lines = [[new InfoSpan(ex.Message)]]);
}
