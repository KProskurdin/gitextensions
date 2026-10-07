using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class ConfirmationsTests
{
    [Test]
    public void Every_confirmation_is_on_the_page_once_with_upstreams_label()
    {
        string designer = UpstreamSource("CommandsDialogs", "SettingsDialog", "Pages",
            "ConfirmationsSettingsPage.Designer.cs");

        Confirmations.Options.Select(option => option.Confirmation).Should()
            .BeEquivalentTo(Enum.GetValues<Confirmation>()).And.OnlyHaveUniqueItems();
        foreach (ConfirmationOption option in Confirmations.Options)
        {
            designer.Should().Contain($".Text = \"{option.Label}\";");
        }
    }

    [Test]
    public void The_questions_are_upstreams()
    {
        string sources = string.Join("\n",
            UpstreamSource("CommandsDialogs", "FormCommit.cs"),
            UpstreamSource("CommandsDialogs", "FormPull.cs"),
            UpstreamSource("CommandsDialogs", "FormPush.cs"),
            UpstreamSource("CommandsDialogs", "FormDeleteBranch.cs"),
            UpstreamSource("UserControls", "RevisionGrid", "RevisionGridControl.cs"),
            UpstreamSource("MessageBoxes.cs"),
            UpstreamSource("TranslatedStrings.cs"));

        // Single-line texts appear as they are; the multi-line ones by their lines.
        string[] texts =
        [
            Confirmations.RebaseQuestion, Confirmations.FetchAndPruneQuestion, Confirmations.StashDropQuestion,
            Confirmations.SwitchWorktreeQuestion, Confirmations.BranchCheckoutQuestion.Replace("\"", "\"\""),
            "You are about to rewrite history.", "Only use Amend if the commit has not been published yet!",
            "You are not working on a branch",
            "This commit will be unreferenced when switching to another branch and can be lost.",
            "The branch you are about to push seems to be a new branch for the remote.",
            "Did you know you can use reflog to restore deleted branches?",
            "The selected branch(es) have not been merged into HEAD.",
        ];
        foreach (string text in texts)
        {
            sources.Should().Contain(text);
        }

        sources.Should().Contain($"&{Confirmations.DontShowAgain}");
    }

    private static string UpstreamSource(params string[] path)
        => File.ReadAllText(Path.Combine([RepositoryRoot(), "src", "app", "GitUI", .. path]));

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
