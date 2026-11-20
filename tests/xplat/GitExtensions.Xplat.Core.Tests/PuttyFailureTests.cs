using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// Upstream FormRemoteProcess's and FormClone's handling of plink's failures, and its FormPuttyError.
internal sealed class PuttyFailureTests
{
    private const string RepositoryPath = "/work/one";
    private const string AuthenticationFailed = "FATAL ERROR: No supported authentication methods available";
    private const string HostKeyNotCached =
        "The server's host key is not cached in the registry. You have no guarantee";
    private const string TrustHost = "If you trust this host, enter \"y\" to add the key to PuTTY's cache";

    private FakeGitOperations _git = null!;
    private RepositoryOperationsViewModel _actions = null!;
    private FakePuttyPrompts _prompts = null!;
    private Dictionary<string, string> _settings = null!;

    [SetUp]
    public void SetUp()
    {
        _git = new FakeGitOperations();
        _prompts = new FakePuttyPrompts();
        _settings = [];
        _actions = new RepositoryOperationsViewModel(_git)
        {
            PuttyPrompts = _prompts,
            UsesPlink = () => true,
            ReadGitSetting = (_, key) => _settings.GetValueOrDefault(key, ""),
            WriteGitSetting = (_, key, value) => _settings[key] = value,
        };
    }

    [TestCase(AuthenticationFailed, PuttyFailureKind.Authentication)]
    [TestCase("FATAL ERROR: Network error", PuttyFailureKind.None)]
    [TestCase(TrustHost, PuttyFailureKind.FingerprintNotRegistered)]
    [TestCase(HostKeyNotCached, PuttyFailureKind.HostKeyNotCached)]
    [TestCase("the SERVER'S HOST KEY is not cached in the registry", PuttyFailureKind.HostKeyNotCached)]
    [TestCase("fatal: repository not found", PuttyFailureKind.None)]
    public void Classify_should_read_plinks_output_as_upstream(string output, PuttyFailureKind expected)
    {
        PuttyFailure.Classify(output).Should().Be(expected);
    }

    [Test]
    public void Classify_should_put_a_failed_authentication_first_as_upstream()
    {
        PuttyFailure.Classify($"{HostKeyNotCached}\n{AuthenticationFailed}").Should()
            .Be(PuttyFailureKind.Authentication);
    }

    [Test]
    public async Task RecoverAsync_should_ask_upstreams_questions_and_say_whether_to_run_again()
    {
        _prompts.Key = new PuttyKeyAnswer("me.ppk");
        (await PuttyFailure.RecoverAsync(_prompts, PuttyFailureKind.Authentication, () => "url")).Should()
            .Be(new PuttyRecovery(true, "me.ppk"));
        _prompts.Key = null;
        (await PuttyFailure.RecoverAsync(_prompts, PuttyFailureKind.Authentication, () => "url")).Should()
            .Be(PuttyRecovery.None);

        (await PuttyFailure.RecoverAsync(_prompts, PuttyFailureKind.HostKeyNotCached, () => null)).Should()
            .Be(PuttyRecovery.None, "upstream asks only with a URL");
        _prompts.Asked.Should().Equal("key", "key");

        _prompts.Connects = false;
        (await PuttyFailure.RecoverAsync(_prompts, PuttyFailureKind.HostKeyNotCached, () => "host")).Retry.Should()
            .BeFalse("upstream runs again only when plink's window ended well");
        _prompts.Answer = false;
        (await PuttyFailure.RecoverAsync(_prompts, PuttyFailureKind.FingerprintNotRegistered, () => "host")).Should()
            .Be(PuttyRecovery.None);
        _prompts.Answer = true;
        (await PuttyFailure.RecoverAsync(_prompts, PuttyFailureKind.FingerprintNotRegistered, () => "host")).Retry
            .Should()
            .BeTrue("upstream restarts after the fingerprint whatever plink returned");

        _prompts.Asked.Should().Equal("key", "key", "CacheHostKey", "connect host", "RegisterFingerprint",
            "RegisterFingerprint", "connect host");
    }

    [Test]
    public async Task A_failed_authentication_should_ask_for_a_key_keep_it_for_the_remote_and_run_again()
    {
        _prompts.Key = new PuttyKeyAnswer(@"C:\keys\me.ppk");

        Task<bool> fetch = _actions.FetchAsync(RepositoryPath, "origin", prune: false);
        _git.Fail(0, new GitOperationException(AuthenticationFailed));
        await WaitUntilAsync(() => _git.Count == 2);
        _git.NameAt(1).Should().Be("Fetch");
        _git.Complete(1);

        (await fetch).Should().BeTrue();
        _prompts.Asked.Should().Equal("key");
        _settings["remote.origin.puttykeyfile"].Should().Be(@"C:\keys\me.ppk".ConvertPathToGitSetting());
    }

    [Test]
    public async Task A_key_already_kept_for_the_remote_should_stay()
    {
        _settings["remote.origin.puttykeyfile"] = "old.ppk";
        _prompts.Key = new PuttyKeyAnswer("new.ppk");

        Task<bool> push = _actions.PushTagsAsync(RepositoryPath, "origin");
        _git.Fail(0, new GitOperationException(AuthenticationFailed));
        await WaitUntilAsync(() => _git.Count == 2);
        _git.Complete(1);

        (await push).Should().BeTrue();
        _settings["remote.origin.puttykeyfile"].Should().Be("old.ppk");
    }

    [Test]
    public async Task A_host_key_not_cached_should_connect_to_the_remotes_url_and_run_again_with_a_clear_output()
    {
        _settings["remote.origin.url"] = "git@example.com:me/repo.git";

        Task<bool> fetch = _actions.FetchAsync(RepositoryPath, "origin", prune: false);
        _git.OutputAt(0)!.Report(new GitOutputLine(HostKeyNotCached, IsProgress: false));
        _git.Fail(0, new GitOperationException("fatal: Could not read from remote repository."));
        await WaitUntilAsync(() => _git.Count == 2);
        _actions.OutputLines.Should().BeEmpty("upstream's Retry clears the output");
        _git.Complete(1);

        (await fetch).Should().BeTrue();
        _prompts.Asked.Should().Equal("CacheHostKey", "connect git@example.com:me/repo.git");
    }

    [Test]
    public async Task A_clone_should_connect_to_the_url_it_clones()
    {
        Task<bool> clone = _actions.CloneAsync("ssh://example.com/repo.git", "/work/repo");
        _git.Fail(0, new GitOperationException(HostKeyNotCached));
        await WaitUntilAsync(() => _git.Count == 2);
        _git.Complete(1);

        (await clone).Should().BeTrue();
        _prompts.Asked.Should().Equal("CacheHostKey", "connect ssh://example.com/repo.git");
    }

    [Test]
    public async Task Cancel_or_another_ssh_client_should_leave_the_failure()
    {
        Task<bool> cancelled = _actions.FetchAsync(RepositoryPath, "origin", prune: false);
        _git.Fail(0, new GitOperationException(AuthenticationFailed));
        (await cancelled).Should().BeFalse();
        _actions.ErrorMessage.Should().Be(AuthenticationFailed);

        _actions.UsesPlink = () => false;
        _prompts.Key = new PuttyKeyAnswer(null);
        Task<bool> openSsh = _actions.FetchAsync(RepositoryPath, "origin", prune: false);
        _git.Fail(1, new GitOperationException(AuthenticationFailed));
        (await openSsh).Should().BeFalse();

        _prompts.Asked.Should().Equal("key");
        _git.Count.Should().Be(2);
    }

    [Test]
    public async Task The_key_window_should_retry_with_the_key_Pageant_took()
    {
        List<string> given = [];
        string? error = "Is PuTTY installed?";
        string? picked = null;
        FakeDialogHost host = new();
        PuttyErrorViewModel viewModel = new(host, () => Task.FromResult(picked), path =>
        {
            given.Add(path);
            return error;
        });

        await viewModel.LoadSshKeyCommand.ExecuteAsync(null);
        given.Should().BeEmpty("nothing was picked");
        picked = "me.ppk";
        await viewModel.LoadSshKeyCommand.ExecuteAsync(null);
        (host.IsClosed, host.Messages.Single()).Should().Be((false, error));
        error = null;
        await viewModel.LoadSshKeyCommand.ExecuteAsync(null);

        given.Should().Equal("me.ppk", "me.ppk");
        (host.IsClosed, viewModel.Answer).Should().Be((true, new PuttyKeyAnswer("me.ppk")));
    }

    [Test]
    public void The_key_windows_Retry_and_Cancel_should_answer_as_upstreams_dialog_results()
    {
        PuttyErrorViewModel retry = new(new FakeDialogHost(), () => Task.FromResult<string?>(null), _ => null);
        retry.RetryCommand.Execute(null);
        retry.Answer.Should().Be(new PuttyKeyAnswer(null));

        FakeDialogHost host = new();
        PuttyErrorViewModel cancel = new(host, () => Task.FromResult<string?>(null), _ => null);
        cancel.CancelCommand.Execute(null);
        (host.IsClosed, cancel.Answer).Should().Be((true, (PuttyKeyAnswer?)null));
    }

    [Test]
    public void The_texts_are_upstreams()
    {
        string sources = string.Join("\n", UpstreamSource("GitUI", "MessageBoxes.cs"),
            UpstreamSource("GitUI", "HelperDialogs", "FormRemoteProcess.cs"),
            UpstreamSource("GitUI", "FormPuttyError.Designer.cs"));

        sources.Should().Contain(PuttyFailure.HostKeyNotCachedText.Replace("\n", "\\n"))
            .And.Contain($"\"{PuttyFailure.HostKeyNotCachedCaption}\"")
            .And.Contain($"\"{PuttyFailure.FingerprintNotRegisteredCaption}\"")
            .And.Contain($"Name = \"{PuttyErrorViewModel.Category}\"");
        foreach (string line in PuttyFailure.FingerprintNotRegisteredText.Split(Environment.NewLine)
                     .Where(line => line.Length > 0))
        {
            sources.Should().Contain(line);
        }

        foreach (string text in (string[])["Authentication error", "Please load your SSH private key",
                     "You must authenticate to run this command.", "Load SSH key", "Retry", "Cancel"])
        {
            sources.Should().Contain($"\"{text}\"");
        }
    }

    // The operation runs again after the questions, once their answers have come through.
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition was not met in time.");
            }

            await Task.Delay(10);
        }
    }

    private static string UpstreamSource(params string[] path)
        => File.ReadAllText(Path.Combine([RepositoryRoot(), "src", "app", .. path]));

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));

    // Records what was asked: "key", the question, or "connect" and the URL.
    private sealed class FakePuttyPrompts : IPuttyPrompts
    {
        public List<string> Asked { get; } = [];

        public PuttyKeyAnswer? Key { get; set; }

        public bool Answer { get; set; } = true;

        public bool Connects { get; set; } = true;

        public Task<PuttyKeyAnswer?> AskForKeyAsync()
        {
            Asked.Add("key");
            return Task.FromResult(Key);
        }

        public Task<bool> ConfirmAsync(PuttyQuestion question)
        {
            Asked.Add(question.ToString());
            return Task.FromResult(Answer);
        }

        public Task<bool> ConnectAsync(string url)
        {
            Asked.Add($"connect {url}");
            return Task.FromResult(Connects);
        }
    }
}
