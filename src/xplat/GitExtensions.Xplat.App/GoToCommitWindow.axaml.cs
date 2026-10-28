using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormGoToCommit</c> (rules in <see cref="GoToCommit"/>): a commit expression, a tag or a branch to go to.
///  The control last focused supplies the revision, as upstream's <c>SetSelectedRevisionByFocusedControl</c>. Closes with
///  the commit git resolves it to (zero when git knows none) after Go, with null otherwise.
/// </summary>
public partial class GoToCommitWindow : Window
{
    private readonly GitModule _module;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IReadOnlyList<IGitRef> _tags = [];
    private IReadOnlyList<IGitRef> _branches = [];
    private Control? _source;
    private bool _pickedInDropDown;
    private bool _going;

    public GoToCommitWindow(string repositoryPath)
    {
        _module = GitModules.Open(repositoryPath);
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormGoToCommit");
        InitialFocus.Attach(this, () => textboxCommitExpression);
        foreach (Control control in new Control[] { textboxCommitExpression, comboBoxTags, comboBoxBranches })
        {
            control.GotFocus += (_, _) => _source = control;
        }

        foreach (ComboBox combo in new[] { comboBoxTags, comboBoxBranches })
        {
            // Upstream's SelectionChangeCommitted: a ref picked in the drop-down goes at once.
            combo.SelectionChanged += (_, _) => _pickedInDropDown |= combo.IsDropDownOpen && combo.SelectedItem is not null;
            combo.DropDownClosed += (_, _) =>
            {
                if (_pickedInDropDown)
                {
                    _source = combo;
                    Go();
                }
            };
            combo.KeyUp += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    Go();
                }
            };
        }

        goButton.Click += (_, _) => Go();
        linkGitRevParse.Click += (_, _) => Run(() => Launcher.LaunchUriAsync(new Uri(GoToCommit.GitRevParseUrl)));
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  Completes when the tags and branches are listed and the clipboard was read, for tests.
    /// </summary>
    public Task RefsLoaded => _loaded.Task;

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));

    // Upstream's FormGoToCommit_Load: the clipboard's text when it names a commit, the tags and the branches.
    private async Task LoadAsync()
    {
        string loading = UpstreamTranslation.ByText(CreateWorktreeDialog.LoadingData);
        comboBoxTags.PlaceholderText = comboBoxBranches.PlaceholderText = loading;
        await SetCommitExpressionFromClipboardAsync();
        (_tags, _branches) = await Task.Run(() =>
            (GoToCommit.Refs(_module, RefsFilter.Tags), GoToCommit.Refs(_module, RefsFilter.Heads)));
        comboBoxTags.PlaceholderText = comboBoxBranches.PlaceholderText = null;
        comboBoxTags.ItemsSource = _tags.Select(tag => tag.LocalName).ToList();
        comboBoxBranches.ItemsSource = _branches.Select(branch => branch.LocalName).ToList();
        _loaded.TrySetResult();
    }

    // Upstream's SetCommitExpressionFromClipboard.
    private async Task SetCommitExpressionFromClipboardAsync()
    {
        string text = (Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : null)?.Trim() ?? "";
        if (text.Length > 0 && !(await Task.Run(() => GoToCommit.Resolve(_module, text))).IsZero)
        {
            textboxCommitExpression.Text = text;
            textboxCommitExpression.SelectAll();
        }
    }

    // Upstream's Go: the revision of the control that supplies it, as git resolves it.
    private void Go()
    {
        if (_going)
        {
            return;
        }

        _going = true;
        string revision = _source == comboBoxTags ? RefHash(_tags, comboBoxTags.Text)
            : _source == comboBoxBranches ? RefHash(_branches, comboBoxBranches.Text)
            : textboxCommitExpression.Text?.Trim() ?? "";
        Run(async () => Close(await Task.Run(() => GoToCommit.Resolve(_module, revision))));
    }

    private static string RefHash(IReadOnlyList<IGitRef> refs, string? name)
        => refs.FirstOrDefault(gitRef => gitRef.LocalName == name)?.Guid ?? "";
}
