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
    public void HasGlobalGitConfig_should_find_a_gitconfig_or_gits_XDG_file_as_upstream()
    {
        string home = Path.Join("root", "home");
        string xdg = Path.Join(home, ".config");

        HomeSettings.HasGlobalGitConfig(home, Lookup([home], [Path.Join(home, ".gitconfig")])).Should().BeTrue();
        HomeSettings.HasGlobalGitConfig(home, Lookup([home, xdg], [Path.Join(xdg, "git", "config")])).Should().BeTrue();
        HomeSettings.HasGlobalGitConfig(home, Lookup([home, xdg], [Path.Join(xdg, "git", "config")],
            new Dictionary<string, string> { ["XDG_CONFIG_HOME"] = "elsewhere" })).Should().BeFalse();
        HomeSettings.HasGlobalGitConfig(home, Lookup([], [Path.Join(home, ".gitconfig")])).Should().BeFalse();
        HomeSettings.HasGlobalGitConfig(null, Lookup([home], [])).Should().BeFalse();
    }

    [Test]
    public void NeedsFix_should_ask_only_when_HOME_is_missing_or_another_folder_has_the_global_config()
    {
        string home = Path.Join("root", "home");
        string profile = Path.Join("root", "profile");
        Dictionary<string, string> variables = new() { ["HOME"] = home, ["USERPROFILE"] = profile };

        HomeSettings.NeedsFix(Lookup([], [], new Dictionary<string, string>())).Should().BeTrue("HOME is not set");
        HomeSettings.NeedsFix(Lookup([profile], [], variables)).Should().BeTrue("HOME is not a folder");
        HomeSettings.NeedsFix(Lookup([home, profile], [Path.Join(home, ".gitconfig")], variables)).Should().BeFalse();
        HomeSettings.NeedsFix(Lookup([home, profile], [Path.Join(profile, ".gitconfig")], variables)).Should().BeTrue();
        HomeSettings.NeedsFix(Lookup([home, profile], [], variables)).Should().BeFalse("no folder is better");
    }

    [Test]
    public void Locate_should_take_the_first_folder_with_a_global_config_in_upstreams_order()
    {
        string user = Path.Join("root", "user");
        string drive = Path.Join("root", "drive");
        string profile = Path.Join("root", "profile");
        string documents = Path.Join("root", "documents");
        Dictionary<string, string> variables = new()
        {
            ["HOMEDRIVE"] = "root", ["HOMEPATH"] = Path.DirectorySeparatorChar + "drive",
            ["USERPROFILE"] = profile,
        };
        string[] folders = [user, drive, profile, documents];
        string Config(string folder) => Path.Join(folder, ".gitconfig");

        HomeSettings.Locate(Lookup(folders, [Config(user), Config(profile)], variables, user, documents)).Should()
            .Be(new HomeLocation(HomeFoundIn.HomeVariable, user));
        HomeSettings.Locate(Lookup(folders, [Config(drive), Config(profile)], variables, user, documents)).Should()
            .Be(new HomeLocation(HomeFoundIn.HomeDriveAndPath, drive));
        HomeLocation fromProfile = HomeSettings.Locate(Lookup(folders, [Config(profile)], variables, user, documents))!;
        (fromProfile.Folder, fromProfile.Choice).Should().Be((profile, HomeChoice.UserProfile));
        HomeLocation fromDocuments = HomeSettings.Locate(Lookup(folders, [Config(documents)], variables, user, documents))!;
        (fromDocuments.Folder, fromDocuments.Choice).Should().Be((documents, HomeChoice.Other));
        HomeSettings.Locate(Lookup(folders, [], variables, user, documents)).Should().BeNull();
    }

    [Test]
    public void The_texts_are_upstreams()
    {
        string sources = string.Join("\n",
            UpstreamSource("GitSettingsPage.cs"), UpstreamSource("GitSettingsPage.Designer.cs"), UpstreamSource("FormFixHome.cs"),
            UpstreamSource("FormFixHome.Designer.cs"), UpstreamSource("SshSettingsPage.Designer.cs"));

        foreach (string text in (string[])[
                     "\"{0} is set to: {1}\"", "\"{0} is not set.\"", $"\"{HomeSettings.NoHomeDirectorySpecified}\"",
                     "\"The environment variable HOME points to a directory that is not accessible:\"",
                     "\"&Use default for HOME\"", "\"&Set HOME to USERPROFILE\"", "\"&Other\"",
                     "\"Specify which ssh client to use\"", "\"OpenSSH\"", "\"PuTTY\"", "\"Other ssh client\"",
                     "\"Path to plink\"", "\"Path to puttygen\"", "\"Path to pageant\"", "\"Configure PuTTY\"",
                     $"\"{HomeSettings.GlobalConfigNotFoundCaption}\"", "\"Download Git\"", SettingsViewModel.DownloadGitUrl,
                     "\"The environment variable HOME does not point to a directory that contains the global git config file:\"",
                     "\"Do you want Git Extensions to help locate the correct folder?\"",
                 ])
        {
            sources.Should().Contain(text);
        }

        foreach (HomeFoundIn foundIn in Enum.GetValues<HomeFoundIn>())
        {
            (string name, string english) = new HomeLocation(foundIn, "folder").Message;
            sources.Should().Contain($"{name} =").And.Contain($"\"{english}\"");
        }
    }

    // Folders that exist, readable files, the process's variables, the user's own HOME and the personal folder.
    private static HomeLookup Lookup(string[] folders, string[] files, Dictionary<string, string>? variables = null,
        string? userHome = null, string? personal = null)
        => new(
            name => variables?.GetValueOrDefault(name),
            name => name == "HOME" ? userHome : null,
            () => personal,
            folders.Contains,
            files.Contains);

    private static string UpstreamSource(string file)
        => File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "CommandsDialogs", "SettingsDialog",
            "Pages", file));

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
