using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

// Upstream FormBrowse's RevisionInfo (the commit info tab or panel, CommitInfoView) and RevisionGpgInfoControl (the GPG
// tab).
public partial class MainWindow
{
    // Upstream's MessageBoxes text.
    private const string CannotFindRevisionText = "Revision \"{0}\" is not visible in the revision grid. Remove the revision filter.";

    private string? _gpgHash;

    private void WireCommitInfo()
    {
        RevisionInfo.Preferences = _preferences;
        RevisionInfo.RevisionLinkClicked += (_, revision) => Run(() => GoToRevisionAsync(revision));
        DetailTabs.SelectionChanged += (_, _) => ShowGpgIfVisible();
    }

    // Upstream's header lists the children the grid has loaded (RevisionGridControl.GetRevisionChildren).
    private void ShowDetails()
    {
        CommitDetails? details = _commits.Details;
        RevisionInfo.RepositoryPath = RepositoryPath;
        RevisionInfo.Show(details, _commits.DetailsError, details is null ? [] : _commits.ChildrenOf(details.Hash));
        ShowGpgTab();
    }

    // Upstream's FormBrowse.RevisionInfo_CommandClicked: the commit, branch or tag is selected in the grid.
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
            ShowRevisionFiltered(hash);
        }
    }

    // Upstream's MessageBoxes.RevisionFilteredInGrid.
    private void ShowRevisionFiltered(string hash)
        => ShowError(string.Format(UpstreamTranslation.ByText(CannotFindRevisionText), hash[..Math.Min(hash.Length, 8)]));

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
