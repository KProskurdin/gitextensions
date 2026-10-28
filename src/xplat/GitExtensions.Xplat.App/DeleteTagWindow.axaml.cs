using Avalonia.Controls;
using GitCommands;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormDeleteTag</c>: deletes the tag named in the box, and, when asked, on the chosen remote too (the
///  branch's remote at first). Closes with true after the delete, as upstream, which closes whether git succeeded or not.
/// </summary>
public partial class DeleteTagWindow : Window
{
    // Upstream's GotoUserManualControl of the form.
    private const string HelpSubfolder = "tag";
    private const string HelpAnchor = "delete-tag";
    private const string HelpTooltip = "Read more about this feature at {0}";

    private readonly Func<string, string?, Task>? _delete;

    public DeleteTagWindow()
        : this(null, [], [], "", null)
    {
    }

    /// <param name="tag">The tag in the box at first, or null.</param>
    /// <param name="tags">The tags (upstream's <c>GetRefs(RefsFilter.Tags)</c>).</param>
    /// <param name="remotes">The remotes upstream's remotes box lists.</param>
    /// <param name="currentRemote">The current branch's remote (upstream's <c>GetCurrentRemote</c>), or empty.</param>
    /// <param name="delete">Deletes the tag, and on the given remote too when it is not null.</param>
    public DeleteTagWindow(string? tag, IReadOnlyList<string> tags, IReadOnlyList<string> remotes, string currentRemote,
        Func<string, string?, Task>? delete)
    {
        _delete = delete;
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormDeleteTag");
        InitialFocus.Attach(this, () => Tags);
        Tags.ItemsSource = tags;
        Tags.Text = tag;
        Remotes.ItemsSource = remotes;
        Remotes.SelectedItem = remotes.FirstOrDefault(remote => remote == currentRemote);
        deleteTag.IsCheckedChanged += (_, _) => Remotes.IsEnabled = deleteTag.IsChecked == true;
        string url = GitUI.UserManual.UserManual.UrlFor(HelpSubfolder, HelpAnchor);
        ToolTip.SetTip(HelpLink, string.Format(UpstreamTranslation.ByText(HelpTooltip), url));
        HelpLink.Click += (_, _) => OsShellUtil.OpenUrlInDefaultBrowser(url);
        Ok.Click += (_, _) => UiActions.Run(DeleteAsync, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
    }

    // Upstream's OkClick: the remote's tag only when the box names one.
    private async Task DeleteAsync()
    {
        string tag = Tags.Text ?? "";
        string? remote = deleteTag.IsChecked == true && tag.Length > 0 ? Remotes.SelectedItem as string ?? "" : null;
        if (_delete is not null)
        {
            await _delete(tag, remote);
        }

        Close(true);
    }
}
