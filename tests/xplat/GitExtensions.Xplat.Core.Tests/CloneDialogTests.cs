using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CloneDialogTests
{
    private const string Url = "https://example.com/owner/project.git";

    private string _folder = "";

    [SetUp]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), "xplat-clone-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [Test]
    public void The_texts_are_upstreams()
    {
        string source = UpstreamSource("src/app/GitUI/CommandsDialogs/FormClone.cs");
        foreach (string text in new[]
                 {
                     CloneDialog.InfoDirectoryExists, CloneDialog.InfoDirectoryNew, CloneDialog.BranchDefaultRemoteHead,
                     CloneDialog.BranchNone, CloneDialog.ErrorDestinationNotSupplied, CloneDialog.ErrorDestinationNotRooted,
                     CloneDialog.ErrorCloneFailed, "The repository will be cloned to a new directory located here:",
                     "Do you want to open the new repository \\\"{0}\\\" now?",
                 })
        {
            source.Should().Contain($"\"{text}\"");
        }

        string designer = UpstreamSource("src/app/GitUI/CommandsDialogs/FormClone.Designer.cs");
        designer.Should().Contain("\"&Destination:\"").And.Contain("\"&Subdirectory to create:\"");
    }

    [Test]
    public void The_repository_names_the_subdirectory()
    {
        CloneDialog dialog = new() { From = Url };
        dialog.Subdirectory.Should().Be("project");

        dialog.From = "";

        dialog.Subdirectory.Should().Be("project");
    }

    [Test]
    public void The_location_line_says_where_the_clone_goes_and_flags_problems()
    {
        CloneDialog dialog = new() { From = Url };
        dialog.LocationInfo().Should().Be(
            (string.Format(CloneDialog.InfoNewRepositoryLocation, Path.Combine("[Destination:]", "project")), true));

        dialog.Destination = _folder;
        dialog.LocationInfo().Should().Be(
            (string.Format(CloneDialog.InfoNewRepositoryLocation, Path.Combine(_folder, "project")) + " (New directory)",
                false));

        Directory.CreateDirectory(Path.Combine(_folder, "project"));
        dialog.LocationInfo().IsError.Should().BeFalse("an empty folder can take the clone");
        File.WriteAllText(Path.Combine(_folder, "project", "f.txt"), "");
        dialog.LocationInfo().Should().Be(
            (string.Format(CloneDialog.InfoNewRepositoryLocation, Path.Combine(_folder, "project"))
             + " (Directory already exists)", true));

        dialog.LocationInfo(text => text == CloneDialog.InfoDirectoryExists ? "(existe)" : text).Text.Should()
            .EndWith(" (existe)");
    }

    [Test]
    public void The_destination_must_be_given_and_absolute()
    {
        CloneDialog dialog = new() { From = Url, Destination = " " };
        dialog.Validate().Should().Be(CloneDialog.ErrorDestinationNotSupplied);

        dialog.Destination = "relative";
        dialog.Validate().Should().Be(CloneDialog.ErrorDestinationNotRooted);

        dialog.Destination = _folder;
        dialog.Validate().Should().BeNull();
        dialog.TargetPath().Should().Be(Path.Combine(_folder, "project"));
    }

    [Test]
    public void Arguments_should_be_upstreams()
    {
        CloneDialog dialog = new() { From = Url };
        dialog.Arguments("/work/project", path => path).ToString().Should()
            .Be($"clone -v --recurse-submodules --progress \"{Url}\" \"/work/project\"");

        dialog.Bare = true;
        dialog.InitializeAllSubmodules = false;
        dialog.DownloadFullHistory = false;
        dialog.Branch = CloneDialog.BranchNone;
        dialog.Arguments("/work/project", path => path).ToString().Should()
            .Be($"clone -v --bare --depth 1 --no-single-branch --progress --no-checkout \"{Url}\" \"/work/project\"");

        dialog.Branch = "develop";
        dialog.Arguments("/work/project", path => path).ToString().Should()
            .Be($"clone -v --bare --depth 1 --no-single-branch --progress --branch develop \"{Url}\" \"/work/project\"");
    }

    [Test]
    public void The_start_uses_the_url_given_else_the_clipboard_else_the_default_destination()
    {
        CloneDialog.Initial(Url, "/clones", "git@example.com:other/repo.git", module: null).Should().Be((Url, "/clones"));
        CloneDialog.Initial(null, "/clones", $"see {Url} please", module: null).Should().Be((Url, "/clones"));
        CloneDialog.Initial(_folder, "/clones", "no url here", module: null).Should().Be(("", _folder));
        CloneDialog.Initial(null, "", null, module: null).Should().Be(("", ""));
    }

    [Test]
    public void TryExtractUrl_finds_the_first_url()
    {
        CloneDialog.TryExtractUrl($"clone {Url} https://example.com/b.git", out string url).Should().BeTrue();
        url.Should().Be(Url);
        CloneDialog.TryExtractUrl("nothing to see", out url).Should().BeFalse();
        url.Should().BeEmpty();
    }

    [Test]
    public void The_destinations_are_the_recent_repositories_parent_folders()
    {
        string first = Directory.CreateDirectory(Path.Combine(_folder, "a")).FullName;
        string second = Directory.CreateDirectory(Path.Combine(_folder, "b")).FullName;

        CloneDialog.DestinationsFromRecent([first, second, Path.Combine(_folder, "gone")]).Should()
            .Equal(Path.GetFullPath(_folder));
    }

    [Test]
    public void The_remote_branches_follow_the_default_choices()
    {
        CloneDialog.DefaultBranchItems.Should().Equal(CloneDialog.BranchDefaultRemoteHead, CloneDialog.BranchNone);
        CloneDialog.BranchItems([]).Should().Equal(CloneDialog.DefaultBranchItems);
    }

    private static string UpstreamSource(string relativePath, [System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", relativePath)));
}
