using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class EditorWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private string _folder = null!;
    private string _path = null!;

    [SetUp]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), "xplat-editor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _path = Path.Combine(_folder, "git-rebase-todo");
        File.WriteAllText(_path, "pick 1111111 first\npick 2222222 second\n");
    }

    [TearDown]
    public void TearDown() => GitProcess.DeleteFolder(_folder);

    [AvaloniaTest]
    public void Closing_without_changes_accepts_the_file_as_it_is()
    {
        EditorWindow editor = OpenEditor();

        Find<TextBox>(editor, "ContentBox").Text.Should().Be("pick 1111111 first\npick 2222222 second\n");
        Find<Button>(editor, "SaveButton").IsEnabled.Should().BeFalse();
        editor.Close();

        editor.IsVisible.Should().BeFalse();
        editor.Accepted.Should().BeTrue();
    }

    [AvaloniaTest]
    public void Save_writes_the_edited_text_and_closing_then_accepts_it()
    {
        EditorWindow editor = OpenEditor();
        Find<TextBox>(editor, "ContentBox").Text = "pick 2222222 second\ndrop 1111111 first\n";

        Click(editor, "SaveButton");
        WaitUntil(() => !Find<Button>(editor, "SaveButton").IsEnabled);
        editor.Close();

        File.ReadAllText(_path).Should().Be("pick 2222222 second\ndrop 1111111 first\n");
        editor.Accepted.Should().BeTrue();
    }

    [AvaloniaTest]
    public void Closing_with_unsaved_changes_asks_and_keeps_the_window_open()
    {
        EditorWindow editor = OpenEditor();
        Find<TextBox>(editor, "ContentBox").Text = "drop 1111111 first\n";

        editor.Close();

        editor.IsVisible.Should().BeTrue();
        Find<Border>(editor, "SaveChangesBar").IsVisible.Should().BeTrue();
        Click(editor, "KeepEditingButton");
        Find<Border>(editor, "SaveChangesBar").IsVisible.Should().BeFalse();
        editor.IsVisible.Should().BeTrue();
        Click(editor, "SaveAndCloseButton");
        WaitUntil(() => !editor.IsVisible);

        File.ReadAllText(_path).Should().Be("drop 1111111 first\n");
        editor.Accepted.Should().BeTrue();
    }

    [AvaloniaTest]
    public void Discarding_the_changes_leaves_the_file_and_rejects_it()
    {
        EditorWindow editor = OpenEditor();
        Find<TextBox>(editor, "ContentBox").Text = "";
        editor.Close();

        Click(editor, "DiscardButton");

        editor.IsVisible.Should().BeFalse();
        editor.Accepted.Should().BeFalse();
        File.ReadAllText(_path).Should().Be("pick 1111111 first\npick 2222222 second\n");
    }

    [AvaloniaTest]
    public void A_missing_file_opens_empty_and_is_created_on_save()
    {
        string newFile = Path.Combine(_folder, "new.txt");
        EditorWindow editor = OpenEditor(newFile);
        Find<TextBox>(editor, "ContentBox").Text = "hello\n";

        Click(editor, "SaveButton");
        WaitUntil(() => File.Exists(newFile));
        editor.Close();

        File.ReadAllText(newFile).Should().Be("hello\n");
    }

    private EditorWindow OpenEditor(string? path = null)
    {
        EditorWindow editor = new(path ?? _path);
        editor.Show();
        WaitUntil(() => editor.FileLoaded.IsCompleted && !Find<TextBox>(editor, "ContentBox").IsReadOnly);
        return editor;
    }

    private static void Click(Window window, string buttonName)
    {
        Find<Button>(window, buttonName).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
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
                Assert.Fail("Timed out waiting for the editor");
            }

            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
    }
}
