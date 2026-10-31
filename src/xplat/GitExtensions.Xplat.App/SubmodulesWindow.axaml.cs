using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using GitCommands;
using GitCommands.Config;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormSubmodules</c> (Repository > Manage submodules...; rules in <see cref="SubmodulesDialog"/>): the
///  submodules with their details, adding one (<see cref="AddSubmoduleWindow"/>), and synchronizing, updating, pulling or
///  removing the selected one.
/// </summary>
public partial class SubmodulesWindow : Window
{
    private readonly GitModule _module;
    private readonly Func<string, Task<bool>> _runGitWithOutput;
    private readonly Func<Window, string, Task> _pull;
    private Task _submodulesLoaded = Task.CompletedTask;

    public SubmodulesWindow()
        : this("", _ => Task.FromResult(false), (_, _) => Task.CompletedTask)
    {
    }

    /// <param name="repositoryPath">The repository whose submodules are managed.</param>
    /// <param name="runGitWithOutput">
    ///  Runs a git command in the repository with its output shown (upstream's FormProcess): adding, synchronizing and
    ///  updating a submodule.
    /// </param>
    /// <param name="pull">Opens the pull dialog on a submodule's work tree (upstream's <c>Pull_Click</c>).</param>
    public SubmodulesWindow(string repositoryPath, Func<string, Task<bool>> runGitWithOutput,
        Func<Window, string, Task> pull)
    {
        _module = GitModules.Open(repositoryPath);
        _runGitWithOutput = runGitWithOutput;
        _pull = pull;
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormSubmodules");
        InitialFocus.Attach(this, () => Submodules);
        new SplitterTracker(this, AppServices.Splitters, "FormSubmodules")
            .Add("splitContainer1", splitContainer1, rows: false, Core.Settings.SplitterFixedPanel.Panel1);
        Submodules.ItemTemplate = new FuncDataTemplate<GitSubmoduleInfo>((info, _) => Row(info));
        Submodules.SelectionChanged += (_, _) => ShowDetails(Submodules.SelectedItem as GitSubmoduleInfo);
        AddSubmodule.Click += (_, _) => Run(AddSubmoduleAsync);
        SynchronizeSubmodule.Click += (_, _) => Run(() => RunOnSelectedAsync(GitCommands.Git.Commands.SubmoduleSync));
        UpdateSubmodule.Click += (_, _) =>
            Run(() => RunOnSelectedAsync(localPath => GitCommands.Git.Commands.SubmoduleUpdate(localPath)));
        RemoveSubmodule.Click += (_, _) => Run(RemoveAsync);
        Pull.Click += (_, _) => Run(PullAsync);
        Opened += (_, _) => Reload();
    }

    /// <summary>
    ///  The last read of the submodules, for tests.
    /// </summary>
    public Task SubmodulesLoaded => _submodulesLoaded;

    /// <summary>
    ///  The submodules as listed.
    /// </summary>
    public IReadOnlyList<GitSubmoduleInfo> Items => Submodules.ItemsSource as IReadOnlyList<GitSubmoduleInfo> ?? [];

    private GitSubmoduleInfo? Selected => Submodules.SelectedItem as GitSubmoduleInfo;

    // Upstream's Initialize: read again, keeping the selected submodule selected (else the first, as its binding source).
    private void Reload() => Run(() => _submodulesLoaded = LoadAsync());

    private async Task LoadAsync()
    {
        string? selectedName = Selected?.Name;
        Cursor = new Cursor(StandardCursorType.Wait);
        try
        {
            IReadOnlyList<GitSubmoduleInfo> submodules = await Task.Run(() => SubmodulesDialog.Load(_module));
            Submodules.ItemsSource = submodules;
            Submodules.SelectedItem = submodules.FirstOrDefault(info => info.Name == selectedName)
                                      ?? submodules.FirstOrDefault();
            ShowDetails(Selected);
        }
        finally
        {
            Cursor = Cursor.Default;
        }
    }

    // Upstream binds the details to the selected row.
    private void ShowDetails(GitSubmoduleInfo? info)
    {
        SubModuleName.Text = info?.Name;
        SubModuleRemotePath.Text = info?.RemotePath;
        SubModuleLocalPath.Text = info?.LocalPath;
        SubModuleCommit.Text = info?.CurrentCommitId.ToString();
        SubModuleBranch.Text = info?.Branch;
        SubModuleStatus.Text = info?.Status;
    }

    // Upstream's AddSubmoduleClick: the add window, then the list read again.
    private async Task AddSubmoduleAsync()
    {
        await new AddSubmoduleWindow(_module.WorkingDir, _runGitWithOutput).ShowDialog(this);
        Reload();
    }

    // Upstream's SynchronizeSubmoduleClick and UpdateSubmoduleClick: the command on the local path, with its output.
    private async Task RunOnSelectedAsync(Func<string, GitExtensions.Extensibility.ArgumentString> command)
    {
        if (Selected is not { } info)
        {
            return;
        }

        await _runGitWithOutput(command(info.LocalPath).ToString());
        Reload();
    }

    // Upstream's RemoveSubmoduleClick: after its warning question; .gitmodules that cannot be read is reported.
    private async Task RemoveAsync()
    {
        if (Selected is not { } info)
        {
            return;
        }

        MessageBoxWindow question = new(UpstreamTranslation.ByText(SubmodulesDialog.RemoveSelectedSubmodule),
            UpstreamTranslation.ByText(SubmodulesDialog.RemoveSelectedSubmoduleCaption), MessageBoxButtons.YesNo);
        await question.ShowDialog(this);
        if (question.Result != DialogResult.Yes)
        {
            return;
        }

        try
        {
            await Task.Run(() => SubmodulesDialog.Remove(_module, info.Name, info.LocalPath));
        }
        catch (GitConfigurationException ex)
        {
            await new MessageBoxWindow(
                string.Format(ResourceManager.TranslatedStrings.GeneralGitConfigExceptionMessage, ex.ConfigPath,
                    Environment.NewLine, (ex.InnerException ?? ex).Message),
                ResourceManager.TranslatedStrings.GeneralGitConfigExceptionCaption, MessageBoxButtons.OK).ShowDialog(this);
            return;
        }

        Reload();
    }

    // Upstream's Pull_Click: the pull dialog on the submodule, then the list read again.
    private async Task PullAsync()
    {
        if (Selected is not { } info)
        {
            return;
        }

        await _pull(this, Path.Combine(_module.WorkingDir, info.LocalPath));
        Reload();
    }

    private static Control Row(GitSubmoduleInfo? info)
    {
        Grid row = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(1, GridUnitType.Star),
                new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = "Status" },
            },
        };
        if (info is null)
        {
            return row;
        }

        TextBlock name = new()
        {
            Text = info.Name,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        TextBlock status = new() { Text = info.Status, Margin = new(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(status, 1);
        row.Children.Add(name);
        row.Children.Add(status);
        return row;
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}
