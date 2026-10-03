using AwesomeAssertions;
using CommonTestUtils;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using NUnit.Framework;

namespace GitCommandsTests.Git;

/// <summary>
///  POSIX counterpart of the remote path case in <c>GitModuleTests_Remotes</c>, which uses 'c:\' and a space in the path.
///  Listing remotes does not query the git version, so this class stages no version output.
/// </summary>
[Platform(Include = "Linux,MacOsX")]
// Mirrors: GitModuleTests
internal sealed class XplatLinuxGitModuleRemotesTests
{
    [Test]
    public async Task GetRemotes_should_keep_posix_fetch_url_with_space()
    {
        MockExecutable executable = new();
        GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), "");
        GitExecutor.TestAccessor executorAccessor = module.GetTestAccessor().Executor;
        executorAccessor.GitExecutable = executable;
        executorAccessor.GitWindowsExecutable = executable;
        executorAccessor.GitCommandRunner = new GitCommandRunner(executable, () => GitModule.SystemEncoding);

        using (executable.StageOutput("remote -v", "with-space\t/srv/Bare Repo (fetch)\nwith-space\t/srv/Bare Repo (push)"))
        {
            IReadOnlyList<GitExtensions.Extensibility.Git.Remote> remotes = await module.GetRemotesAsync();

            remotes.Should().ContainSingle();
            remotes[0].FetchUrl.Should().Be("/srv/Bare Repo");
        }

        executable.Verify();
    }
}
