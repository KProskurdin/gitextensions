using AwesomeAssertions;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CreateBranchDialogTests
{
    private static readonly ObjectId _commit = ObjectId.Parse("0123456789abcdef0123456789abcdef01234567");

    [Test]
    public void The_texts_are_upstreams()
    {
        string form = UpstreamSource("src/app/GitUI/CommandsDialogs/FormCreateBranch.cs");
        foreach (string text in new[]
                 {
                     CreateBranchDialog.NoRevisionSelected, CreateBranchDialog.BranchNameIsEmpty,
                     CreateBranchDialog.BranchNameIsNotValid, CreateBranchDialog.CreatingOrphanBranch,
                 })
        {
            form.Should().Contain($"\"{text}\"");
        }

        string summary = UpstreamSource("src/app/GitUI/UserControls/CommitSummaryUserControl.cs");
        summary.Should().Contain($"\"{CreateBranchDialog.NoRevision}\"").And.Contain($"\"{CreateBranchDialog.NotAvailable}\"");
        UpstreamSource("src/app/GitUI/UserControls/CommitSummaryUserControl.Designer.cs").Should()
            .Contain($"\"{CreateBranchDialog.TagsCaption}\"").And.Contain($"\"{CreateBranchDialog.BranchesCaption}\"");
        UpstreamSource("src/app/GitUI/UserControls/CommitPickerSmallControl.cs").Should()
            .Contain($"\"{CreateBranchDialog.InvalidCommitHash}\"");
        UpstreamSource("src/app/GitUI/TranslatedStrings.cs").Should().Contain($"_error = new(\"{CreateBranchDialog.ErrorCaption}\")");
    }

    [Test]
    public void A_repository_without_commits_can_only_create_a_checked_out_orphan()
    {
        CreateBranchDialog dialog = new(hasCommits: false);

        dialog.IsOrphanOnly.Should().BeTrue();
        dialog.CreateOrphan.Should().BeTrue();
        dialog.CheckoutAfterCreate.Should().BeTrue();
        dialog.CanChangeOrphan.Should().BeFalse();
        dialog.CanChangeCheckout.Should().BeFalse();
        dialog.CanClearOrphan.Should().BeTrue();

        dialog.SetCreateOrphan(false);

        dialog.CreateOrphan.Should().BeTrue();
    }

    [Test]
    public void An_orphan_is_checked_out_and_takes_no_commit()
    {
        CreateBranchDialog dialog = new(hasCommits: true) { CheckoutAfterCreate = false };
        dialog.CanClearOrphan.Should().BeFalse();

        dialog.SetCreateOrphan(true);

        dialog.CheckoutAfterCreate.Should().BeTrue();
        dialog.CanChangeCheckout.Should().BeFalse();
        dialog.CanChangeRevision.Should().BeFalse();
        dialog.CanClearOrphan.Should().BeTrue();
    }

    [Test]
    public void ProposeName_should_take_the_first_branch_else_the_first_tag_unless_a_name_was_given()
    {
        GitModule module = GitModules.Open(TestContext.CurrentContext.WorkDirectory);
        GitRef tag = new(module, _commit, "refs/tags/v1");
        GitRef branch = new(module, _commit, "refs/remotes/origin/feature/x", "origin");

        CreateBranchDialog fromRefs = new(hasCommits: true);
        fromRefs.ProposeName([tag, branch]);
        fromRefs.BranchName.Should().Be("feature/x");

        CreateBranchDialog fromTag = new(hasCommits: true);
        fromTag.ProposeName([tag]);
        fromTag.BranchName.Should().Be("v1");

        CreateBranchDialog inFolder = new(hasCommits: true, namePrefix: "team/");
        inFolder.ProposeName([branch]);
        inFolder.BranchName.Should().Be("team/");
    }

    [Test]
    public void Validate_should_ask_for_a_commit_a_name_and_a_name_git_accepts()
    {
        CreateBranchDialog dialog = new(hasCommits: true) { BranchName = "feature" };

        dialog.Validate(_ => true).Should().Be(CreateBranchDialog.NoRevisionSelected);

        dialog.Revision = _commit;
        dialog.BranchName = "  ";
        dialog.Validate(_ => true).Should().Be(CreateBranchDialog.BranchNameIsEmpty);

        dialog.BranchName = " bad..name ";
        dialog.Validate(_ => false, english => $"<{english}>").Should().Be("<“bad..name” is not valid branch name.>");

        dialog.BranchName = "feature";
        dialog.Validate(name => name == "feature").Should().BeNull();
    }

    [Test]
    public void Commands_should_be_upstreams()
    {
        CreateBranchDialog dialog = new(hasCommits: true) { BranchName = " feature ", Revision = _commit };

        dialog.Commands().Select(command => command.ToString()).Should()
            .Equal($"checkout -b \"feature\" {_commit}");

        dialog.CheckoutAfterCreate = false;
        dialog.Commands().Select(command => command.ToString()).Should().Equal($"branch \"feature\" {_commit}");

        dialog.SetCreateOrphan(true);
        dialog.ClearOrphan = true;
        dialog.Commands().Select(command => command.ToString()).Should().Equal("checkout --orphan feature", "rm --force -r .");
    }

    private static string UpstreamSource(string relativePath, [System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", relativePath)));
}
