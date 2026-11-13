using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
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
    public void A_file_in_a_repository_is_read_and_saved_in_its_files_encoding()
    {
        using TestRepository repo = new();
        repo.Run("config", "i18n.filesEncoding", "iso-8859-1");
        string file = Path.Combine(repo.Path, "latin1.txt");
        File.WriteAllBytes(file, [0x63, 0x61, 0x66, 0xE9, (byte)'\n']);

        EditorWindow editor = OpenEditor(file);

        Find<TextBox>(editor, "ContentBox").Text.Should().Be("caf\u00E9\n");
        Find<TextBox>(editor, "ContentBox").Text = "caf\u00E9 noir\n";
        Click(editor, "SaveButton");
        WaitUntil(() => !Find<Button>(editor, "SaveButton").IsEnabled);
        File.ReadAllBytes(file).Should().Equal(System.Text.Encoding.Latin1.GetBytes("caf\u00E9 noir\n"));
        editor.Close();
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

    [AvaloniaTest]
    public void Find_takes_the_word_at_the_caret_and_selects_each_occurrence_around_the_end()
    {
        EditorWindow editor = OpenEditor();
        TextBox box = Find<TextBox>(editor, "ContentBox");
        box.CaretIndex = 1;

        Press(editor, DiffCommand.Find);

        FindWindow find = editor.FindWindow!;
        find.IsVisible.Should().BeTrue();
        find.ReplaceMode.Should().BeFalse();
        find.LookFor.Should().Be("pick");
        Find<Button>(find, "btnHighlightAll").IsVisible.Should().BeFalse("a text box cannot mark every occurrence");

        Click(find, "btnFindNext");
        WaitUntil(() => box.SelectionStart == 19);
        box.SelectedText.Should().Be("pick");
        Click(find, "btnFindNext");
        WaitUntil(() => box.SelectionStart == 0);
        Click(find, "btnFindPrevious");
        WaitUntil(() => box.SelectionStart == 19);
        editor.Close();
    }

    [AvaloniaTest]
    public void Replace_changes_the_selection_only_when_it_is_the_text_looked_for_then_moves_on()
    {
        EditorWindow editor = OpenEditor();
        TextBox box = Find<TextBox>(editor, "ContentBox");
        box.SelectionStart = 0;
        box.SelectionEnd = 4;

        Press(editor, DiffCommand.Replace);

        FindWindow find = editor.FindWindow!;
        find.ReplaceMode.Should().BeTrue();
        find.Title.Should().StartWith("Find & replace");
        find.LookFor.Should().Be("pick");
        find.ReplaceWith = "very pick";
        Click(find, "btnReplace");

        WaitUntil(() => box.SelectionStart == 24);
        box.Text.Should().Be("very pick 1111111 first\npick 2222222 second\n");
        box.SelectedText.Should().Be("pick", "upstream goes on to the next one, past what it just wrote");
        Find<Button>(editor, "SaveButton").IsEnabled.Should().BeTrue();
        DiscardAndClose(editor);
    }

    [AvaloniaTest]
    public void Replace_All_keeps_to_a_region_tells_the_count_closes_and_can_be_undone_at_once()
    {
        EditorWindow editor = OpenEditor();
        TextBox box = Find<TextBox>(editor, "ContentBox");
        box.Text = "pick a\npick b\npick c\n";
        box.SelectionStart = 7;
        box.SelectionEnd = 20;

        Press(editor, DiffCommand.Replace);

        FindWindow find = editor.FindWindow!;
        find.Title.Should().Contain("selection only");
        find.LookFor = "pick";
        find.ReplaceWith = "drop";
        Click(find, "btnReplaceAll");

        WaitUntil(() => find.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow message = find.OwnedWindows.OfType<MessageBoxWindow>().Single();
        message.Close();
        WaitUntil(() => !find.IsVisible);
        box.Text.Should().Be("pick a\ndrop b\ndrop c\n");
        box.Undo();
        box.Text.Should().Be("pick a\npick b\npick c\n");
        DiscardAndClose(editor);
    }

    [AvaloniaTest]
    public void Replace_All_says_so_when_there_is_nothing_and_leaves_the_text()
    {
        EditorWindow editor = OpenEditor();
        Press(editor, DiffCommand.Replace);
        FindWindow find = editor.FindWindow!;
        find.LookFor = "nothing-like-this";
        Click(find, "btnReplaceAll");

        WaitUntil(() => find.OwnedWindows.OfType<MessageBoxWindow>().Any());
        find.OwnedWindows.OfType<MessageBoxWindow>().Single().Close();
        Find<TextBox>(editor, "ContentBox").Text.Should().Be("pick 1111111 first\npick 2222222 second\n");
        find.IsVisible.Should().BeTrue();
        editor.Close();
    }

    [AvaloniaTest]
    public void Go_to_line_moves_the_caret_to_the_line()
    {
        EditorWindow editor = OpenEditor();

        Press(editor, DiffCommand.GoToLine);

        WaitUntil(() => editor.OwnedWindows.OfType<GoToLineWindow>().Any());
        GoToLineWindow window = editor.OwnedWindows.OfType<GoToLineWindow>().Single();
        window.FindControl<NumericUpDown>("_NO_TRANSLATE_LineNumberUpDown")!.Value = 2;
        Click(window, "okBtn");
        WaitUntil(() => Find<TextBox>(editor, "ContentBox").CaretIndex == 19);
        editor.Close();
    }

    [AvaloniaTest]
    public void The_box_menu_offers_find_replace_and_go_to_line_with_their_keys()
    {
        EditorWindow editor = OpenEditor();

        List<MenuItem> items = [.. ((MenuFlyout)Find<TextBox>(editor, "ContentBox").ContextFlyout!).Items.OfType<MenuItem>()];

        items.Select(item => item.Header).Should().Equal("Cut", "Copy", "Paste", "_Find...", "_Replace...", "Go to line...");
        items[3].InputGesture.Should().Be(Hotkeys.Diff.GestureFor(DiffCommand.Find));
        items[4].InputGesture.Should().Be(Hotkeys.Diff.GestureFor(DiffCommand.Replace));
        editor.Close();
    }

    private static void Press(EditorWindow editor, DiffCommand command)
    {
        KeyGesture gesture = Hotkeys.Diff.GestureFor(command)!;
        editor.KeyPress(gesture.Key, TestKeys.Raw(gesture.KeyModifiers), PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    private static void DiscardAndClose(EditorWindow editor)
    {
        editor.Close();
        Click(editor, "DiscardButton");
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
        UiClicks.Raise(Find<Button>(window, buttonName), Button.ClickEvent);
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
