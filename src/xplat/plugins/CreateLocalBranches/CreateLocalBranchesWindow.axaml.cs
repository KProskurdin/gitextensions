using Avalonia.Controls;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Ui;
using GitExtUtils;

namespace GitExtensions.Plugins.CreateLocalBranches;

/// <summary>
///  The new shell's version of upstream's <c>CreateLocalBranchesForm</c>: creates or updates a local tracking branch for every
///  branch of the remote, with upstream's commands and messages.
/// </summary>
public partial class CreateLocalBranchesWindow : Window
{
    private readonly IGitModule _module;

    public CreateLocalBranchesWindow(IGitModule module)
    {
        _module = module;
        InitializeComponent();
        CreateButton.Click += (_, _) => CreateBranches();
    }

    // Upstream's button1_Click. As upstream, a branch git cannot create (e.g. one that exists) is passed over, and the count
    // in the message is that of every branch git listed, not of those created.
    private void CreateBranches()
    {
        string remote = RemoteBox.Text ?? "";
        string remotePrefix = $"remotes/{remote}/";
        GitArgumentBuilder args = new("branch") { "-a" };
        string[] references = _module.GitExecutable.GetOutput(args).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        WindowOwner owner = new(this);
        if (references.Length == 0)
        {
            MessageBoxes.ShowError(owner, "No remote branches found.");
            Close();
            return;
        }

        foreach (string reference in references)
        {
            string branchName = reference.Trim(Delimiters.GitOutput);
            if (branchName.StartsWith(remotePrefix, StringComparison.Ordinal))
            {
                args = new GitArgumentBuilder("branch")
                {
                    "--track",
                    branchName.Replace(remotePrefix, ""),
                    branchName,
                };
                _module.GitExecutable.Execute(args, throwOnErrorExit: false);
            }
        }

        MessageBoxes.Show(owner, $"{references.Length} local tracking branches have been created/updated.",
            "Information", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
        Close();
    }
}
