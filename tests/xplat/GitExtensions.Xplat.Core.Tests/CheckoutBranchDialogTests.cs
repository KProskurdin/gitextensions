using AwesomeAssertions;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CheckoutBranchDialogTests
{
    private static readonly ObjectId _local = ObjectId.Parse("1111111111111111111111111111111111111111");
    private static readonly ObjectId _remote = ObjectId.Parse("2222222222222222222222222222222222222222");

    [Test]
    public void The_texts_are_upstreams()
    {
        string form = UpstreamSource("src/app/GitUI/CommandsDialogs/FormCheckoutBranch.cs").Replace("\r\n", "\n");
        foreach (string text in new[]
                 {
                     CheckoutBranchDialog.CustomBranchNameIsEmpty, CheckoutBranchDialog.CustomBranchNameIsNotValid,
                     CheckoutBranchDialog.CreateBranchText, CheckoutBranchDialog.ApplyStashedItemsAgainCaption,
                     CheckoutBranchDialog.ApplyStashedItemsAgain, CheckoutBranchDialog.ResetNonFastForwardBranch,
                     CheckoutBranchDialog.ResetCaption, CheckoutBranchDialog.InvalidBranchName,
                 })
        {
            form.Should().Contain($"\"{text.Replace("\n", "\\n")}\"");
        }

        UpstreamSource("src/app/GitUI/CommandsDialogs/FormCheckoutBranch.Designer.cs").Should()
            .Contain($"\"{CheckoutBranchDialog.ResetBranchText}\"");
    }

    [Test]
    public void A_remote_branch_proposes_its_tracking_branch_and_a_custom_name_that_does_not_exist()
    {
        CheckoutBranchDialog dialog = new(Source(), LocalChangesAction.DontChange, createLocalBranchForRemote: false,
            "origin/feature", remote: true);

        dialog.Branches.Should().Contain("origin/feature");
        dialog.RemoteName.Should().Be("origin");
        dialog.LocalBranchName.Should().Be("feature");
        dialog.ResetOptionText.Should().Be(CheckoutBranchDialog.ResetBranchText);
        dialog.CustomBranchName.Should().Be("origin_feature_2", "origin_feature exists, so upstream numbers the proposal");
        dialog.NewBranchMode.Should().Be(CheckoutNewBranchMode.Reset);

        dialog.Branch = "origin/new";

        dialog.LocalBranchName.Should().Be("new");
        dialog.ResetOptionText.Should().Be(CheckoutBranchDialog.CreateBranchText);
        dialog.CustomBranchName.Should().Be("origin_new");
    }

    [Test]
    public void Switching_the_kind_lists_the_other_branches_and_clears_the_choice()
    {
        CheckoutBranchDialog dialog = new(Source(), LocalChangesAction.DontChange, createLocalBranchForRemote: true, "main");
        dialog.Branches.Should().Equal("main", "feature", "origin_feature", "main");
        dialog.NewBranchMode.Should().Be(CheckoutNewBranchMode.Create);

        dialog.SetRemote(true);

        dialog.Branches.Should().Equal("origin/feature", "origin/new");
        dialog.Branch.Should().BeEmpty();
    }

    [Test]
    public void A_local_branch_is_checked_out_without_the_window_unless_upstreams_settings_ask_for_it()
    {
        CheckoutBranchDialog clean = new(Source(dirty: false), LocalChangesAction.DontChange, false, "feature");
        clean.ChecksOutWithoutWindow(alwaysShow: false, useDefaultAction: false).Should().BeTrue();
        clean.ChecksOutWithoutWindow(alwaysShow: true, useDefaultAction: false).Should().BeFalse();

        CheckoutBranchDialog dirty = new(Source(dirty: true), LocalChangesAction.Stash, false, "feature");
        dirty.ChecksOutWithoutWindow(alwaysShow: false, useDefaultAction: false).Should().BeFalse();
        dirty.ChecksOutWithoutWindow(alwaysShow: false, useDefaultAction: true).Should().BeTrue();
        dirty.EffectiveChanges(shown: false, useDefaultAction: true).Should().Be(LocalChangesAction.Stash);
        dirty.EffectiveChanges(shown: false, useDefaultAction: false).Should().Be(LocalChangesAction.DontChange);

        CheckoutBranchDialog unknown = new(Source(dirty: null), LocalChangesAction.Merge, false, "feature");
        unknown.HasUncommittedChanges.Should().BeTrue("as upstream, an unchecked working directory counts as dirty");

        new CheckoutBranchDialog(Source(dirty: false), LocalChangesAction.DontChange, false, "origin/feature", remote: true)
            .ChecksOutWithoutWindow(alwaysShow: false, useDefaultAction: true).Should().BeFalse();
    }

    [Test]
    public void Reset_is_never_kept_as_the_default_action()
    {
        CheckoutBranchDialog dialog = new(Source(), LocalChangesAction.DontChange, false, "feature")
        {
            ChangesMode = LocalChangesAction.Reset,
            SetAsDefault = true,
        };

        dialog.CanSetAsDefault.Should().BeFalse();
        dialog.NewDefaultAction.Should().BeNull();

        dialog.ChangesMode = LocalChangesAction.Merge;
        dialog.NewDefaultAction.Should().Be(LocalChangesAction.Merge);
    }

    [Test]
    public void Validate_should_ask_for_an_existing_branch_and_a_valid_custom_name()
    {
        CheckoutBranchDialog dialog = new(Source(), LocalChangesAction.DontChange, true, remote: true) { Branch = "origin/nope" };
        dialog.Validate(_ => true).Should().Be(CheckoutBranchDialog.InvalidBranchName);

        dialog.Branch = "origin/feature";
        dialog.CustomBranchName = " ";
        dialog.Validate(_ => true).Should().Be(CheckoutBranchDialog.CustomBranchNameIsEmpty);

        dialog.CustomBranchName = "bad..name";
        dialog.Validate(_ => false).Should().StartWith("“bad..name” is not valid branch name.");

        dialog.CustomBranchName = "mine";
        dialog.Validate(_ => true).Should().BeNull();
    }

    [Test]
    public void Arguments_should_be_upstreams()
    {
        CheckoutBranchDialog local = new(Source(), LocalChangesAction.DontChange, false, "feature");
        local.Arguments(LocalChangesAction.Merge).ToString().Should().Be("checkout --merge \"feature\"");
        local.Arguments(LocalChangesAction.Stash).ToString().Should().Be("checkout \"feature\"");

        CheckoutBranchDialog remote = new(Source(), LocalChangesAction.DontChange, false, "origin/feature", remote: true);
        remote.Arguments(LocalChangesAction.Reset).ToString().Should().Be("checkout --force -B \"feature\" \"origin/feature\"");

        remote.NewBranchMode = CheckoutNewBranchMode.Create;
        remote.CustomBranchName = "mine";
        remote.Arguments(LocalChangesAction.DontChange).ToString().Should().Be("checkout -b \"mine\" --track \"origin/feature\"");

        remote.NewBranchMode = CheckoutNewBranchMode.DontCreate;
        remote.Arguments(LocalChangesAction.DontChange).ToString().Should().Be("checkout \"origin/feature\"");
    }

    [Test]
    public void ResetWarning_is_given_only_when_the_reset_loses_commits()
    {
        CheckoutBranchDialog dialog = new(Source(), LocalChangesAction.DontChange, false, "origin/feature", remote: true);

        dialog.ResetWarning(_local, _remote, mergeBase: _local).Should().BeNull("a fast-forward loses nothing");
        dialog.ResetWarning(_local, _remote, mergeBase: _remote).Should()
            .StartWith("You are going to reset the “feature” branch").And.Contain(_remote.ToShortString());
        dialog.ResetWarning(_local, _remote, mergeBase: default).Should().Contain("since the merge base revision");

        dialog.NewBranchMode = CheckoutNewBranchMode.Create;
        dialog.ResetWarning(_local, _remote, mergeBase: _remote).Should().BeNull();
    }

    private static CheckoutBranchSource Source(bool? dirty = true)
        => new(
            ["main", "feature", "origin_feature"],
            ["origin/feature", "origin/new"],
            ["origin"],
            (remote, branch) => branch[(remote.Length + 1)..],
            dirty);

    private static string UpstreamSource(string relativePath, [System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", relativePath)));
}
