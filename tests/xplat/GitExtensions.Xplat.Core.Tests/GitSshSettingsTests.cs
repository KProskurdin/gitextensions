using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class GitSshSettingsTests
{
    [TestCase("", SshClientKind.OpenSsh)]
    [TestCase(@"C:\Program Files\PuTTY\plink.exe", SshClientKind.Putty)]
    [TestCase(@"C:\Program Files\TortoiseGit\bin\TortoisePlink.exe", SshClientKind.Putty)]
    [TestCase(@"C:\Tools\PLINK.EXE", SshClientKind.Putty)]
    [TestCase("/usr/bin/plink", SshClientKind.Other)]
    [TestCase("/usr/bin/ssh", SshClientKind.Other)]
    public void KindOf_should_read_the_stored_path_as_upstreams_page(string path, SshClientKind expected)
    {
        SshClients.KindOf(path).Should().Be(expected);
    }

    [TestCase(SshClientKind.OpenSsh, "")]
    [TestCase(SshClientKind.Putty, "plink.exe")]
    [TestCase(SshClientKind.Other, "my-ssh")]
    public void PathFor_should_store_what_upstreams_page_stores(SshClientKind kind, string expected)
    {
        SshClients.PathFor(kind, "plink.exe", "my-ssh").Should().Be(expected);
    }

    [Test]
    public void PuttyLocations_should_follow_upstreams_order()
    {
        Dictionary<string, string> variables = new()
        {
            ["GITEXT_PUTTY"] = @"D:\putty", ["ProgramFiles"] = @"C:\PF", ["ProgramFiles(x86)"] = @"C:\PF86",
        };

        SshClients.PuttyLocations(name => variables.GetValueOrDefault(name), is64Bit: true).Should().Equal(
            @"D:\putty", @"C:\PF\PuTTY\", @"C:\PF86\PuTTY\", @"C:\PF\TortoiseGit\bin\", @"C:\PF86\TortoiseGit\bin\",
            @"C:\PF\TortoiseSvn\bin\", @"C:\PF86\TortoiseSvn\bin\");
        SshClients.PuttyLocations(name => variables.GetValueOrDefault(name), is64Bit: false).Should().Equal(
            @"D:\putty", @"C:\PF\PuTTY\", @"C:\PF\TortoiseGit\bin\", @"C:\PF\TortoiseSvn\bin\");
    }

    [Test]
    public void FindPutty_should_fill_the_missing_programs_from_the_first_folder_that_has_them()
    {
        HashSet<string> files =
        [
            @"C:\PF\TortoiseGit\bin\TortoisePlink.exe", @"C:\PF\PuTTY\puttygen.exe", @"C:\PF\PuTTY\pageant.exe",
            @"C:\keep\pageant.exe",
        ];

        PuttyPaths found = SshClients.FindPutty(new PuttyPaths("", "", @"C:\keep\pageant.exe"),
            [@"C:\PF\PuTTY", @"C:\PF\TortoiseGit\bin\"], files.Contains);

        found.Should().Be(new PuttyPaths(@"C:\PF\TortoiseGit\bin\TortoisePlink.exe", @"C:\PF\PuTTY\puttygen.exe",
            @"C:\keep\pageant.exe"));
    }

    [Test]
    public void Describe_should_name_GIT_CONFIG_GLOBAL_when_set_and_HOME_otherwise()
    {
        HomeSettings.Describe(@"C:\cfg\.gitconfig", @"C:\Users\me", windows: true).Should()
            .Be(@"%GIT_CONFIG_GLOBAL% is set to: C:\cfg\.gitconfig");
        HomeSettings.Describe(null, "/home/me", windows: false).Should()
            .Be("$HOME is set to: /home/me    ($GIT_CONFIG_GLOBAL is not set.)");
    }

    [TestCase("", false, HomeChoice.Default)]
    [TestCase("", true, HomeChoice.UserProfile)]
    [TestCase(@"D:\home", true, HomeChoice.Other)]
    public void ChoiceOf_should_read_the_settings_as_upstreams_FormFixHome(string custom, bool userProfile,
        HomeChoice expected)
    {
        HomeSettings.ChoiceOf(custom, userProfile).Should().Be(expected);
    }

    [Test]
    public void Validate_should_refuse_an_empty_other_folder_and_a_HOME_that_does_not_exist()
    {
        HomeSettings.Validate(HomeChoice.Other, "", "", _ => true).Should().Be(HomeSettings.NoHomeDirectorySpecified);
        HomeSettings.Validate(HomeChoice.Other, @"D:\gone", @"D:\gone", _ => false).Should()
            .Be("The environment variable HOME points to a directory that is not accessible:" + Environment.NewLine +
                "\"D:\\gone\"");
        HomeSettings.Validate(HomeChoice.UserProfile, "", @"C:\Users\me", _ => true).Should().BeNull();
    }

    [Test]
    public void The_texts_are_upstreams()
    {
        string sources = string.Join("\n",
            UpstreamSource("GitSettingsPage.cs"), UpstreamSource("FormFixHome.cs"),
            UpstreamSource("FormFixHome.Designer.cs"), UpstreamSource("SshSettingsPage.Designer.cs"));

        foreach (string text in (string[])
                 [
                     "\"{0} is set to: {1}\"", "\"{0} is not set.\"", $"\"{HomeSettings.NoHomeDirectorySpecified}\"",
                     "\"The environment variable HOME points to a directory that is not accessible:\"",
                     "\"&Use default for HOME\"", "\"&Set HOME to USERPROFILE\"", "\"&Other\"",
                     "\"Specify which ssh client to use\"", "\"OpenSSH\"", "\"PuTTY\"", "\"Other ssh client\"",
                     "\"Path to plink\"", "\"Path to puttygen\"", "\"Path to pageant\"", "\"Configure PuTTY\"",
                 ])
        {
            sources.Should().Contain(text);
        }
    }

    private static string UpstreamSource(string file)
        => File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "CommandsDialogs", "SettingsDialog",
            "Pages", file));

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
