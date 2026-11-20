using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit;
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

        Find<TextEditor>(editor, "ContentBox").Text.Should().Be("pick 1111111 first\npick 2222222 second\n");
        Find<Button>(editor, "SaveButton").IsEffectivelyEnabled.Should().BeFalse();
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

        Find<TextEditor>(editor, "ContentBox").Text.Should().Be("caf\u00E9\n");
        Find<TextEditor>(editor, "ContentBox").Text = "caf\u00E9 noir\n";
        Click(editor, "SaveButton");
        WaitUntil(() => !editor.ViewModel.HasChanges);
        File.ReadAllBytes(file).Should().Equal(System.Text.Encoding.Latin1.GetBytes("caf\u00E9 noir\n"));
        editor.Close();
    }

    [AvaloniaTest]
    public void Save_writes_the_edited_text_and_closing_then_accepts_it()
    {
        EditorWindow editor = OpenEditor();
        Find<TextEditor>(editor, "ContentBox").Text = "pick 2222222 second\ndrop 1111111 first\n";

        Click(editor, "SaveButton");

        // The button is off while the save runs too, so the wait is for the file to be written.
        WaitUntil(() => !editor.ViewModel.HasChanges);
        editor.Close();

        File.ReadAllText(_path).Should().Be("pick 2222222 second\ndrop 1111111 first\n");
        editor.Accepted.Should().BeTrue();
    }

    [AvaloniaTest]
    public void Closing_with_unsaved_changes_asks_and_keeps_the_window_open()
    {
        EditorWindow editor = OpenEditor();
        Find<TextEditor>(editor, "ContentBox").Text = "drop 1111111 first\n";

        editor.Close();

        editor.IsVisible.Should().BeTrue();
        Find<Border>(editor, "SaveChangesBar").IsVisible.Should().BeTrue();
        Click(editor, "KeepEditingButton");
        Find<Border>(editor, "SaveChangesBar").IsVisible.Should().BeFalse();
        editor.IsVisible.Should().BeTrue();
        editor.Close();
        Click(editor, "SaveAndCloseButton");
        WaitUntil(() => !editor.IsVisible);

        File.ReadAllText(_path).Should().Be("drop 1111111 first\n");
        editor.Accepted.Should().BeTrue();
    }

    [AvaloniaTest]
    public void Discarding_the_changes_leaves_the_file_and_rejects_it()
    {
        EditorWindow editor = OpenEditor();
        Find<TextEditor>(editor, "ContentBox").Text = "";
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
        Find<TextEditor>(editor, "ContentBox").Text = "hello\n";

        Click(editor, "SaveButton");
        WaitUntil(() => File.Exists(newFile));
        editor.Close();

        File.ReadAllText(newFile).Should().Be("hello\n");
    }

    [AvaloniaTest]
    public void The_rebase_todo_shows_line_numbers_and_upstreams_highlighting_which_follows_the_text()
    {
        EditorWindow editor = OpenEditor();
        TextEditor box = Find<TextEditor>(editor, "ContentBox");

        box.ShowLineNumbers.Should().BeTrue();
        box.TextArea.TextView.LineTransformers.OfType<EditorColorizer>().Should().ContainSingle();
        EditorColorizer.ColorOf(GitExtensions.Xplat.Core.Editing.EditorTokenKind.Drop, dark: false).Should()
            .Be(System.Drawing.Color.Red, "upstream's light color for drop");

        box.Text = "drop 1111111 first\n";
        editor.ViewModel.Text.Should().Be("drop 1111111 first\n", "the editor's text goes to the view model");
        DiscardAndClose(editor);
    }

    [AvaloniaTest]
    public void A_commit_message_gets_upstreams_waves_with_their_tooltips_which_follow_the_text()
    {
        string file = Path.Combine(_folder, "COMMIT_EDITMSG");
        string summary = new('s', 55);
        File.WriteAllText(file, summary + "\nno blank line before me\n");
        EditorWindow editor = OpenEditor(file);
        TextEditor box = Find<TextEditor>(editor, "ContentBox");

        EditorColorizer colorizer = editor.Colorizer!;
        colorizer.Marks.Select(mark => mark.ToolTip).Should().Equal(
            GitExtensions.Xplat.Core.Editing.EditorHighlighting.SummaryTooLongText,
            GitExtensions.Xplat.Core.Editing.EditorHighlighting.SpacerNeededText);
        colorizer.ToolTipAt(box.Document, 52).Should().Be(GitExtensions.Xplat.Core.Editing.EditorHighlighting.SummaryTooLongText);
        colorizer.ToolTipAt(box.Document, 10).Should().BeNull();

        box.Text = "Short summary\n\nBody\n";
        colorizer.Marks.Should().BeEmpty("the marks follow the text");
        DiscardAndClose(editor);
    }

    [AvaloniaTest]
    public void Another_file_is_highlighted_in_the_syntax_of_its_extension()
    {
        string code = Path.Combine(_folder, "Program.cs");
        File.WriteAllText(code, "// comment\nclass C { }\n");
        EditorWindow editor = OpenEditor(code);
        editor.Colorizer.Should().NotBeNull("the file viewer's C# definition");
        editor.Close();

        string plain = Path.Combine(_folder, "notes.unknownextension");
        File.WriteAllText(plain, "text\n");
        EditorWindow other = OpenEditor(plain);
        other.Colorizer.Should().BeNull();
        other.Close();
    }

    [AvaloniaTest]
    public void Find_takes_the_word_at_the_caret_and_selects_each_occurrence_around_the_end()
    {
        EditorWindow editor = OpenEditor();
        TextEditor box = Find<TextEditor>(editor, "ContentBox");
        box.CaretOffset = 1;

        Press(editor, DiffCommand.Find);

        FindWindow find = editor.FindWindow!;
        find.IsVisible.Should().BeTrue();
        find.ReplaceMode.Should().BeFalse();
        find.LookFor.Should().Be("pick");
        Find<Button>(find, "btnHighlightAll").IsVisible.Should().BeTrue("the editor marks every occurrence, as upstream's");

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
    public void Highlight_all_marks_every_occurrence_closes_and_the_marks_move_with_the_text()
    {
        EditorWindow editor = OpenEditor();
        TextEditor box = Find<TextEditor>(editor, "ContentBox");
        Press(editor, DiffCommand.Find);
        FindWindow find = editor.FindWindow!;
        find.LookFor = "pick";

        Click(find, "btnHighlightAll");

        editor.Highlighted.Should().Equal((0, 4), (19, 4));
        find.IsVisible.Should().BeFalse("upstream closes the window when it marked some");
        box.Document.Insert(0, "# note\n");
        editor.Highlighted.Should().Equal((7, 4), (26, 4));
        DiscardAndClose(editor);
    }

    [AvaloniaTest]
    public void Replace_changes_the_selection_only_when_it_is_the_text_looked_for_then_moves_on()
    {
        EditorWindow editor = OpenEditor();
        TextEditor box = Find<TextEditor>(editor, "ContentBox");
        box.Select(0, 4);

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
        Find<Button>(editor, "SaveButton").IsEffectivelyEnabled.Should().BeTrue();
        DiscardAndClose(editor);
    }

    [AvaloniaTest]
    public void Replace_All_keeps_to_a_region_tells_the_count_closes_and_can_be_undone_at_once()
    {
        EditorWindow editor = OpenEditor();
        TextEditor box = Find<TextEditor>(editor, "ContentBox");
        box.Text = "pick a\npick b\npick c\n";
        box.Select(7, 13);

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
        Find<TextEditor>(editor, "ContentBox").Text.Should().Be("pick 1111111 first\npick 2222222 second\n");
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
        WaitUntil(() => Find<TextEditor>(editor, "ContentBox").CaretOffset == 19);
        editor.Close();
    }

    [AvaloniaTest]
    public void The_box_menu_offers_find_replace_and_go_to_line_with_their_keys()
    {
        EditorWindow editor = OpenEditor();

        List<MenuItem> items = [.. ((MenuFlyout)Find<TextEditor>(editor, "ContentBox").ContextFlyout!).Items.OfType<MenuItem>()];

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
        WaitUntil(() => editor.FileLoaded.IsCompleted && !Find<TextEditor>(editor, "ContentBox").IsReadOnly);
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
