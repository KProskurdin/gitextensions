using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class RepositoryTextFileWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private string _folder = null!;

    [SetUp]
    public void SetUp()
        => _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "xplat-files-" + Guid.NewGuid().ToString("N")))
            .FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [AvaloniaTest]
    public void Save_writes_the_text_with_a_final_line_break_and_closes()
    {
        File.WriteAllText(Path.Combine(_folder, ".mailmap"), "Old <old@example.com>\n");
        (Window owner, RepositoryTextFileWindow window, Task<bool> result) = Open(RepositoryTextFiles.MailMap);
        window.Title.Should().Be("Edit .mailmap");
        Find<TextBlock>(window, "label1").Text.Should().StartWith("Edit the mailmap.");
        TextBox text = Find<TextBox>(window, "FileText");
        text.Text.Should().Be("Old <old@example.com>\n");
        text.Text = "New <new@example.com>";

        Click(window, "Save");

        WaitUntil(() => result.IsCompleted);
        result.Result.Should().BeTrue();
        File.ReadAllText(Path.Combine(_folder, ".mailmap")).Should().Be("New <new@example.com>" + Environment.NewLine);
        owner.Close();
    }

    [AvaloniaTest]
    public void Closing_with_changes_asks_and_Cancel_keeps_the_window_and_No_drops_them()
    {
        (Window owner, RepositoryTextFileWindow window, Task<bool> result) = Open(RepositoryTextFiles.GitAttributes);
        window.Title.Should().Be("Edit .gitattributes");
        Find<TextBox>(window, "FileText").Text = "*.jpg binary";

        window.Close();
        Answer(window, "Save changes?", "Cancel");
        result.IsCompleted.Should().BeFalse();

        window.Close();
        Answer(window, "Save changes?", "No");

        WaitUntil(() => result.IsCompleted);
        result.Result.Should().BeFalse();
        File.Exists(Path.Combine(_folder, ".gitattributes")).Should().BeFalse();
        owner.Close();
    }

    private (Window Owner, RepositoryTextFileWindow Window, Task<bool> Result) Open(RepositoryTextFile file)
    {
        Window owner = new();
        owner.Show();
        RepositoryTextFileWindow window = new(_folder, file);
        Task<bool> result = window.ShowDialog<bool>(owner);
        WaitUntil(() => window.FileLoaded.IsCompleted);
        return (owner, window, result);
    }

    private static void Answer(Window window, string caption, string button)
    {
        WaitUntil(() => window.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow question = window.OwnedWindows.OfType<MessageBoxWindow>().Single();
        question.Title.Should().Be(caption);
        question.GetLogicalDescendants().OfType<Button>().Single(choice => choice.Name == button + "Button")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => !question.IsVisible);
    }

    private static void Click(Window window, string name)
        => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static T Find<T>(Window window, string name)
        where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"No control named {name}");

    private static void WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the UI");
            }

            Dispatcher.UIThread.RunJobs();
        }
    }
}
