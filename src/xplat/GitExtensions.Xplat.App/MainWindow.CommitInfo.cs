using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

// Upstream's CommitInfo and CommitInfoHeader (the commit info tab or panel) and RevisionGpgInfoControl (the GPG tab).
public partial class MainWindow
{
    // Upstream's CommitInfo and MessageBoxes texts.
    private const string CopyLinkText = "Copy &link ({0})";
    private const string CannotFindRevisionText = "Revision \"{0}\" is not visible in the revision grid. Remove the revision filter.";

    private CancellationTokenSource? _revisionInfoLoad;
    private IReadOnlyList<InfoHeaderRow> _headerRows = [];
    private bool _showAllBranches;
    private bool _showAllTags;
    private string? _commitInfoLink;
    private string? _gpgHash;

    private void WireCommitInfo()
    {
        DetailMessage.LinkClicked += (_, link) => OpenInfoLink(link);
        DetailRevisionInfo.LinkClicked += (_, link) => OpenInfoLink(link);

        // The link under the pointer, read before the menu opens (it opens while the request bubbles).
        RevisionInfo.AddHandler(ContextRequestedEvent, (_, e) => _commitInfoLink = LinkAt(e), RoutingStrategies.Tunnel);
        CommitInfoContextMenu.Opening += (_, _) => ShowCommitInfoMenu();
        CopyCommitInfoLinkMenuItem.Click += (_, _) => Run(() => CopyTextAsync(_commitInfoLink ?? ""));
        CopyCommitInfoMenuItem.Click += (_, _) => Run(() => CopyTextAsync(CommitInfoPlainText()));
        ShowContainedInBranchesMenuItem.Click += (_, _) => SetCommitInfoOptions(options => options with
        {
            BranchesLocal = !options.BranchesLocal
        });
        ShowContainedInBranchesRemoteMenuItem.Click += (_, _) => SetCommitInfoOptions(options => options with
        {
            BranchesRemote = !options.BranchesRemote
        });
        ShowContainedInBranchesRemoteIfNoLocalMenuItem.Click += (_, _) => SetCommitInfoOptions(options => options with
        {
            BranchesRemoteIfNoLocal = !options.BranchesRemoteIfNoLocal
        });
        ShowContainedInTagsMenuItem.Click += (_, _) => SetCommitInfoOptions(options => options with
        {
            Tags = !options.Tags
        });
        ShowAnnotatedTagsMessagesMenuItem.Click += (_, _) => SetCommitInfoOptions(options => options with
        {
            AnnotatedTagsMessages = !options.AnnotatedTagsMessages
        });
        ShowDerivesFromTagMenuItem.Click += (_, _) => SetCommitInfoOptions(options => options with
        {
            DerivesFromTag = !options.DerivesFromTag
        });
        DetailTabs.SelectionChanged += (_, _) => ShowGpgIfVisible();
    }

    private void ShowDetails()
    {
        CommitDetails? details = _commits.Details;
        ShowDetailAvatar();
        _showAllBranches = false;
        _showAllTags = false;
        if (details is null)
        {
            ShowHeader([]);
            DetailMessage.Lines = string.IsNullOrEmpty(_commits.DetailsError) ? [] : [[new InfoSpan(_commits.DetailsError)]];
            DetailRevisionInfo.Lines = [];
            ShowGpgTab();
            return;
        }

        // Upstream's header lists the children the grid has loaded (RevisionGridControl.GetRevisionChildren).
        if (details.Data is { } data)
        {
            data.ChildIds = [.. _commits.ChildrenOf(details.Hash).Select(ObjectId.Parse)];
            ShowHeader(CommitInfoText.Header(data));
        }
        else
        {
            ShowHeader([]);
        }

        DetailMessage.Lines = details.MessageLines ?? CommitInfoText.Parse(WebUtility.HtmlEncode(details.Message));
        Run(LoadRevisionInfoAsync);
        ShowGpgTab();
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
        if (_commits.Details is not { } details || RepositoryPath is not { } path)
        {
            return;
        }

        CommitInfoRequest request = new(path, details.Hash, _preferences.CommitInfoOptions, RefSorting(),
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

    // Upstream's LinkFactory.ExecuteLink and FormBrowse.RevisionInfo_CommandClicked: internal links select the commit,
    // branch or tag in the grid or show the whole list, other links open in the browser.
    private void OpenInfoLink(string link)
    {
        if (!CommitInfoText.TryParseCommand(link, out string command, out string data))
        {
            if (Uri.TryCreate(link, UriKind.Absolute, out Uri? address))
            {
                Run(() => Launcher.LaunchUriAsync(address));
            }

            return;
        }

        switch (command)
        {
            case "gotocommit":
            case "gotobranch":
            case "gototag":
                Run(() => GoToRevisionAsync(data));
                break;
            case "showall":
                _showAllBranches |= data == "branches";
                _showAllTags |= data == "tags";
                Run(LoadRevisionInfoAsync);
                break;
        }
    }

    private async Task GoToRevisionAsync(string revision)
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        string? hash = ObjectId.TryParse(revision, out ObjectId id)
            ? id.ToString()
            : await RepositoryScriptContext.ResolveCommitAsync(path, revision);
        if (hash is null)
        {
            return;
        }

        int index = _commits.VisibleRows.ToList()
            .FindIndex(item => string.Equals(item.Row.Hash, hash, StringComparison.OrdinalIgnoreCase));
        if (!SelectGridRow(index >= 0 ? index : null))
        {
            // As upstream, a commit the filters hide, such as a second parent with "first parent only".
            ShowError(string.Format(UpstreamTranslation.ByText(CannotFindRevisionText), hash[..Math.Min(hash.Length, 8)]));
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
        CommitInfoOptions options = _preferences.CommitInfoOptions;
        ShowContainedInBranchesMenuItem.IsChecked = options.BranchesLocal;
        ShowContainedInBranchesRemoteMenuItem.IsChecked = options.BranchesRemote;
        ShowContainedInBranchesRemoteIfNoLocalMenuItem.IsChecked = options.BranchesRemoteIfNoLocal;
        ShowContainedInTagsMenuItem.IsChecked = options.Tags;
        ShowAnnotatedTagsMessagesMenuItem.IsChecked = options.AnnotatedTagsMessages;
        ShowDerivesFromTagMenuItem.IsChecked = options.DerivesFromTag;
    }

    private void SetCommitInfoOptions(Func<CommitInfoOptions, CommitInfoOptions> change)
    {
        _preferences.CommitInfoOptions = change(_preferences.CommitInfoOptions);
        _preferences.Save();
        Run(LoadRevisionInfoAsync);
    }

    // Upstream's "Copy commit info": the header without relative dates, children and parents, then the message.
    private string CommitInfoPlainText()
        => $"{CommitInfoText.HeaderPlainText(_headerRows)}{Environment.NewLine}{Environment.NewLine}"
           + DetailMessage.PlainText.Replace("\n", Environment.NewLine);

    private async Task CopyTextAsync(string text)
    {
        if (GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    // Upstream's FillGpgInfoAsync: the tab is there for real commits while ShowGpgInformation is on, and reads the
    // signatures when it is shown.
    private void ShowGpgTab()
    {
        CommitDetails? details = _commits.Details;
        bool show = details is not null && !ArtificialCommits.IsArtificial(details.Hash) && _preferences.ShowGpgInformation;
        GpgInfoTab.IsVisible = show;
        if (!show && DetailTabs.SelectedItem == GpgInfoTab)
        {
            DetailTabs.SelectedItem = DiffTab;
        }

        ShowGpgIfVisible();
    }

    private void ShowGpgIfVisible()
    {
        if (DetailTabs.SelectedItem != GpgInfoTab || _commits.Details is not { } details || details.Hash == _gpgHash)
        {
            return;
        }

        _gpgHash = details.Hash;
        Run(() => LoadGpgAsync(details.Hash));
    }

    private async Task LoadGpgAsync(string hash)
    {
        if (RepositoryPath is not { } path || await CommitInfoReader.LoadGpgAsync(path, hash) is not { } display
                                           || hash != _gpgHash)
        {
            return;
        }

        CommitSignImage.Source = UpstreamImages.Named(display.CommitImage);
        CommitGpgText.Text = display.CommitText;
        TagSignImage.Source = UpstreamImages.Named(display.TagImage);
        TagGpgText.Text = display.TagText ?? "";
        bool tag = display.TagText is not null;
        TagSignImage.IsVisible = tag;
        TagGpgText.IsVisible = tag;
        GpgInfo.RowDefinitions[1].Height = tag ? GridLength.Star : new GridLength(0);
    }
}
