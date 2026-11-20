using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Upstream FormRemoteProcess's windows after a plink failure: FormPuttyError and the host key and fingerprint questions.
internal sealed class PuttyPromptsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    [AvaloniaTest]
    public async Task The_key_window_has_upstreams_texts_and_Retry_retries_without_a_key()
    {
        Window owner = new();
        owner.Show();
        Task<PuttyKeyAnswer?> asked = new PuttyPrompts(() => owner).AskForKeyAsync();
        PuttyErrorWindow window = Owned<PuttyErrorWindow>(owner);

        window.Title.Should().Be("Authentication error");
        Find<TextBlock>(window, "lblPleaseLoadKey").Text.Should().Be("Please load your SSH private key");
        Find<TextBlock>(window, "lblMustAuthenticate").Text.Should().Be("You must authenticate to run this command.");
        Find<Button>(window, "LoadSSHKey").Command.Should().NotBeNull();
        UiClicks.Click(Find<Button>(window, "Retry"));
        WaitUntil(() => asked.IsCompleted);

        (await asked).Should().Be(new PuttyKeyAnswer(null));
        owner.Close();
    }

    [AvaloniaTest]
    public async Task Cancel_in_the_key_window_gives_up()
    {
        Window owner = new();
        owner.Show();
        Task<PuttyKeyAnswer?> asked = new PuttyPrompts(() => owner).AskForKeyAsync();

        UiClicks.Click(Find<Button>(Owned<PuttyErrorWindow>(owner), "Cancel"));
        WaitUntil(() => asked.IsCompleted);

        (await asked).Should().BeNull();
        owner.Close();
    }

    [AvaloniaTest]
    public async Task The_host_key_question_is_upstreams_and_Yes_answers_it()
    {
        Window owner = new();
        owner.Show();
        Task<bool> asked = new PuttyPrompts(() => owner).ConfirmAsync(PuttyQuestion.CacheHostKey);
        MessageBoxWindow question = Owned<MessageBoxWindow>(owner);

        question.Title.Should().Be(PuttyFailure.HostKeyNotCachedCaption);
        question.GetLogicalDescendants().OfType<Button>().Single(button => button.Name == "YesButton")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => asked.IsCompleted);

        (await asked).Should().BeTrue();
        owner.Close();
    }

    [AvaloniaTest]
    public async Task The_fingerprint_question_is_upstreams_and_No_answers_it()
    {
        Window owner = new();
        owner.Show();
        Task<bool> asked = new PuttyPrompts(() => owner).ConfirmAsync(PuttyQuestion.RegisterFingerprint);
        MessageBoxWindow question = Owned<MessageBoxWindow>(owner);

        question.Title.Should().Be(PuttyFailure.FingerprintNotRegisteredCaption);
        question.GetLogicalDescendants().OfType<Button>().Single(button => button.Name == "NoButton")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => asked.IsCompleted);

        (await asked).Should().BeFalse();
        owner.Close();
    }

    private static T Owned<T>(Window owner) where T : Window
    {
        WaitUntil(() => owner.OwnedWindows.OfType<T>().Any());
        return owner.OwnedWindows.OfType<T>().Single();
    }

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"Control {name} not found");

    private static void WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition was not met in time.");
            }

            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }
}
