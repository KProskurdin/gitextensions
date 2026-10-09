using Avalonia.Controls;
using GitExtensions.Xplat.Ui;
using Brushes = Avalonia.Media.Brushes;
using DialogResult = System.Windows.Forms.DialogResult;
using TreeView = Avalonia.Controls.TreeView;

namespace TeamCityIntegration.Settings;

/// <summary>
///  The new shell's version of upstream's <c>TeamCityBuildChooser</c>: the server's project tree from upstream's adapter,
///  the builds of a project read when it is first expanded, and the chosen build's project and ID.
/// </summary>
public partial class TeamCityBuildChooser : Window, IDialogWindow
{
    private const string LoadingText = "Loading...";

    private readonly TeamCityAdapter _teamCityAdapter = new();
    private TreeViewItem? _previouslySelectedProject;

    /// <summary>
    ///  Reads the project tree at once, as upstream's constructor does; a server that cannot be read throws.
    /// </summary>
    public TeamCityBuildChooser(string teamCityServerUrl, string teamCityProjectName, string teamCityBuildIdFilter)
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, nameof(TeamCityBuildChooser));
        TeamCityProjectName = teamCityProjectName;
        TeamCityBuildIdFilter = teamCityBuildIdFilter;
        _teamCityAdapter.InitializeHttpClient(teamCityServerUrl);
        if (_teamCityAdapter.GetProjectsTree() is { } rootProject)
        {
            TreeViewItem root = ProjectItem(rootProject);
            treeViewTeamCityProjects.Items.Add(root);
            root.IsExpanded = true;
        }

        treeViewTeamCityProjects.SelectionChanged += (_, _) => buttonOK.IsEnabled = SelectedBuild is not null;
        treeViewTeamCityProjects.DoubleTapped += (_, _) => SelectBuild();
        buttonOK.Click += (_, _) => SelectBuild();
        buttonCancel.Click += (_, _) => Close();
        Opened += (_, _) => ReselectPreviouslySelectedBuild();
    }

    public string TeamCityProjectName { get; private set; }

    public string TeamCityBuildIdFilter { get; private set; }

    public DialogResult DialogResult { get; private set; } = DialogResult.Cancel;

    private Build? SelectedBuild => (treeViewTeamCityProjects.SelectedItem as TreeViewItem)?.Tag as Build;

    private void ReselectPreviouslySelectedBuild()
    {
        if (_previouslySelectedProject is null)
        {
            return;
        }

        _previouslySelectedProject.IsExpanded = true;
        treeViewTeamCityProjects.SelectedItem = _previouslySelectedProject.Items.OfType<TreeViewItem>()
            .FirstOrDefault(item => item.Name == TeamCityBuildIdFilter) ?? _previouslySelectedProject;
    }

    // Upstream's ConvertProjectInTreeNode: a project without sub-projects shows "Loading..." until it is expanded.
    private TreeViewItem ProjectItem(Project project)
    {
        TreeViewItem item = new() { Header = project.Name, Name = project.Name, Tag = project };
        foreach (TreeViewItem subProject in (project.SubProjects ?? []).Select(ProjectItem)
                     .OrderBy(subProject => subProject.Name, StringComparer.Ordinal))
        {
            item.Items.Add(subProject);
        }

        if (item.ItemCount == 0)
        {
            item.Items.Add(new TreeViewItem { Header = LoadingText });
        }

        if (TeamCityProjectName == project.Id)
        {
            _previouslySelectedProject = item;
        }

        item.PropertyChanged += (_, e) =>
        {
            if (e.Property == TreeViewItem.IsExpandedProperty && item.IsExpanded)
            {
                LoadProjectBuilds(item);
            }
        };
        return item;
    }

    // Upstream's LoadProjectBuilds, on the first expansion.
    private void LoadProjectBuilds(TreeViewItem item)
    {
        Project project = (Project)item.Tag!;
        if (project.Builds is not null || project.Id is null)
        {
            return;
        }

        project.Builds = _teamCityAdapter.GetProjectBuilds(project.Id);
        if (item.ItemCount == 1 && item.Items[0] is TreeViewItem { Tag: null } loading)
        {
            item.Items.Remove(loading);
        }

        foreach (Build build in project.Builds.OrderBy(build => build.Id, StringComparer.Ordinal))
        {
            item.Items.Add(new TreeViewItem
            {
                Header = build.DisplayName,
                Name = build.Id,
                Tag = build,
                Foreground = Brushes.RoyalBlue,
            });
        }
    }

    private void SelectBuild()
    {
        if (SelectedBuild is { ParentProject: { } parentProject, Id: { } id })
        {
            TeamCityProjectName = parentProject;
            TeamCityBuildIdFilter = id;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
