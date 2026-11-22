using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Platform;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class WindowsGitTests
{
    private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _folders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _environment = [];
    private readonly HashSet<string> _runs = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _tried = [];

    [SetUp]
    public void SetUp()
    {
        _files.Clear();
        _folders.Clear();
        _environment.Clear();
        _runs.Clear();
        _tried.Clear();
        _environment["ProgramFiles"] = @"C:\PF";
        _environment["ProgramFiles(x86)"] = @"C:\PF86";
    }

    [Test]
    public void The_texts_are_upstreams()
    {
        string page = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "CommandsDialogs",
            "SettingsDialog", "Pages", "ChecklistSettingsPage.cs"));
        foreach (FieldInfo field in typeof(WindowsGit).GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(field => field.IsLiteral))
        {
            page.Should().Contain($"\"{field.GetRawConstantValue()}\"", field.Name);
        }
    }

    [Test]
    public void GitLocations_should_be_upstreams_in_its_order()
    {
        _environment["GITEXT_GIT"] = @"D:\mygit\";

        WindowsGit.GitLocations(Lookup(installed: @"E:\Git\")).Should().Equal(@"D:\mygit\", @"E:\Git\", @"C:\PF\Git\",
            @"C:\PF86\Git\", @"C:\PF\msysgit\", @"C:\PF86\msysgit\", @"C:\msysgit\", @"C:\cygwin\",
            @"C:\Users\me\AppData\Local\Programs\Git\");
    }

    [Test]
    public void SolveGitCommand_should_take_the_stored_command_first_when_it_runs()
    {
        _files.Add(@"D:\tools\git.exe");
        _folders.Add(@"C:\PF\Git\bin\");
        _runs.UnionWith([@"D:\tools\git.exe", @"C:\PF\Git\bin\git.exe"]);

        WindowsGit.SolveGitCommand(@"D:\tools\git.exe", null, Lookup()).Should().Be(@"D:\tools\git.exe");
    }

    [Test]
    public void SolveGitCommand_should_try_each_locations_bin_then_cmd_then_the_PATH()
    {
        _folders.UnionWith([@"C:\PF\Git\bin\", @"C:\PF\Git\cmd\"]);
        _runs.Add("git");

        WindowsGit.SolveGitCommand(@"D:\gone\git.exe", null, Lookup()).Should().Be("git");

        _tried.Should().Equal(@"C:\PF\Git\bin\git.exe", @"C:\PF\Git\cmd\git.exe", @"C:\PF\Git\cmd\git.cmd", "git");
    }

    [Test]
    public void SolveGitCommand_should_be_null_when_no_git_runs()
        => WindowsGit.SolveGitCommand("", null, Lookup()).Should().BeNull();

    [TestCase(@"C:\PF\Git\cmd\git.exe", @"C:\PF\Git\usr\bin\")]
    [TestCase(@"C:\PF\Git\bin\git.exe", @"C:\PF\Git\usr\bin\")]
    public void SolveLinuxToolsDir_should_find_sh_next_to_git(string gitCommand, string expected)
    {
        _folders.Add(@"C:\PF\Git\usr\bin\");
        _files.Add(@"C:\PF\Git\usr\bin\sh.exe");

        WindowsGit.SolveLinuxToolsDir(gitCommand, "", Lookup()).Should().Be(expected);
    }

    [Test]
    public void SolveLinuxToolsDir_should_take_bin_when_usr_bin_has_no_sh()
    {
        _folders.Add(@"C:\cygwin\bin\");
        _files.Add(@"C:\cygwin\bin\sh.exe");

        WindowsGit.SolveLinuxToolsDir(@"C:\cygwin\bin\git.exe", "", Lookup()).Should().Be(@"C:\cygwin\bin\");
    }

    [Test]
    public void SolveLinuxToolsDir_should_keep_the_stored_folder_or_none_when_sh_is_on_the_PATH()
    {
        _environment["PATH"] = @"C:\tools;C:\other";
        _files.Add(@"C:\other\sh.exe");
        _folders.Add(@"D:\linux\");
        _files.Add(@"D:\linux\sh");

        WindowsGit.SolveLinuxToolsDir("git", @"D:\linux\", Lookup()).Should().Be(@"D:\linux\");
        WindowsGit.SolveLinuxToolsDir("git", @"D:\gone\", Lookup()).Should().BeEmpty();
    }

    [Test]
    public void SolveLinuxToolsDir_should_be_null_without_sh()
        => WindowsGit.SolveLinuxToolsDir(@"C:\PF\Git\cmd\git.exe", "", Lookup()).Should().BeNull();

    private WindowsGitLookup Lookup(string? installed = null)
        => new()
        {
            Environment = name => _environment.GetValueOrDefault(name),
            FileExists = _files.Contains,
            DirectoryExists = _folders.Contains,
            InstalledGit = () => installed,
            LocalApplicationData = @"C:\Users\me\AppData\Local",
            Is64BitProcess = true,
            CanRun = command =>
            {
                _tried.Add(command);
                return _runs.Contains(command);
            },
        };

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
