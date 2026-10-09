using Avalonia;
using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Plugins.GitImpact;

/// <summary>
///  The new shell's version of upstream's <c>FormImpact</c>: the impact graph of the repository, the author under the pointer
///  with their commits and changed lines, and whether submodules count. As upstream's scroll bar, the view keeps to the
///  newest weeks while the history loads, unless it was scrolled away from them.
/// </summary>
public partial class ImpactWindow : Window
{
    private const string Category = "FormImpact";
    private static string AuthorCommits => UpstreamTranslation.Text(Category, "_authorCommits", "{0} ({1} Commits, {2} Changed Lines)");

    private double _distanceFromEnd;

    public ImpactWindow(IGitModule module)
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, Category);
        Graph.Init(module);
        Graph.GraphChanged += (_, _) => ShowAuthor(Graph.SelectedAuthor);

        // New weeks widen the graph: the view keeps its distance from the newest week. Only a scroll changes that distance.
        GraphScroll.ScrollChanged += (_, e) =>
        {
            if (e.ExtentDelta.X != 0 || e.ViewportDelta.X != 0)
            {
                GraphScroll.Offset = new Vector(
                    Math.Max(0, GraphScroll.Extent.Width - GraphScroll.Viewport.Width - _distanceFromEnd), 0);
            }
            else if (e.OffsetDelta.X != 0)
            {
                _distanceFromEnd = Math.Max(0,
                    GraphScroll.Extent.Width - GraphScroll.Viewport.Width - GraphScroll.Offset.X);
            }
        };
        cbIncludingSubmodules.IsCheckedChanged += (_, _) => Graph.ShowSubmodules = cbIncludingSubmodules.IsChecked == true;
        Opened += (_, _) => Graph.UpdateData();
        Closed += (_, _) =>
        {
            Graph.Stop();
            Graph.Dispose();
        };
    }

    // Upstream's UpdateAuthorInfo.
    private void ShowAuthor(string author)
    {
        bool shown = !string.IsNullOrEmpty(author);
        lblAuthor.IsVisible = AuthorColor.IsVisible = shown;
        if (shown)
        {
            ImpactLoader.DataPoint data = Graph.GetAuthorInfo(author);
            lblAuthor.Text = string.Format(AuthorCommits, author, data.Commits, data.ChangedLines);
            AuthorColor.Background = Graph.GetAuthorBrush(author);
        }
    }
}
