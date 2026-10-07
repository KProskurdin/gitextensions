using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Scripts;
using GitUI.ScriptsEngine;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class ScriptListEditorTests
{
    [Test]
    public void Add_should_give_a_new_enabled_script_upstreams_next_identifier_and_name()
    {
        ScriptListEditor empty = new([]);
        ScriptListEditor filled = new([new ScriptDefinition { Name = "a", HotkeyCommandIdentifier = 9004 }]);

        ScriptListItem first = empty.Add();
        ScriptListItem next = filled.Add();

        // Upstream adds the new script before taking the maximum, so an empty list starts at 9001, not 9000.
        first.Script.HotkeyCommandIdentifier.Should().Be(9001);
        next.Script.HotkeyCommandIdentifier.Should().Be(9005);
        next.Script.Should().BeEquivalentTo(new ScriptDefinition
        {
            Name = ScriptListEditor.NewScriptName, Enabled = true, HotkeyCommandIdentifier = 9005
        });
        filled.Items.Should().HaveCount(2).And.EndWith(next);
    }

    [Test]
    public void Moves_should_reorder_the_scripts_and_stop_at_the_ends()
    {
        ScriptListEditor editor = new([new ScriptDefinition { Name = "a" }, new ScriptDefinition { Name = "b" }, new ScriptDefinition { Name = "c" }]);
        ScriptListItem a = editor.Items[0];
        ScriptListItem c = editor.Items[2];

        editor.MoveUp(a);
        editor.MoveDown(a);
        editor.MoveDown(c);
        editor.MoveUp(c);

        editor.Scripts.Select(script => script.Name).Should().Equal("b", "c", "a");
        editor.CanMoveUp(editor.Items[0]).Should().BeFalse();
        editor.CanMoveDown(editor.Items[2]).Should().BeFalse();
    }

    [Test]
    public void Edits_should_change_copies_until_the_scripts_are_stored()
    {
        ScriptDefinition stored = new() { Name = "stored", Command = "git" };
        ScriptListEditor editor = new([stored]);

        editor.Items[0].Name = "edited";
        editor.Remove(editor.Items[0]);

        stored.Name.Should().Be("stored");
        editor.Scripts.Should().BeEmpty();
    }

    [Test]
    public void A_changed_value_should_announce_the_list_texts()
    {
        ScriptListItem item = new ScriptListEditor([new ScriptDefinition { Name = "a", Command = "git" }]).Items[0];
        List<string?> changed = [];
        ((INotifyPropertyChanged)item).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        item.Arguments = "status";
        item.OnEvent = ScriptEvent.AfterPull;

        item.CommandLine.Should().Be("git status");
        item.EventText.Should().Be("AfterPull");
        changed.Should().Contain([nameof(ScriptListItem.CommandLine), nameof(ScriptListItem.EventText)]);
    }

    [Test]
    public void ArgumentsHelp_should_be_upstreams_text()
    {
        string source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "CommandsDialogs", "SettingsDialog",
            "Pages", "ScriptsSettingsPage.cs"));
        Match upstream = Regex.Match(source, "_scriptSettingsPageHelpDisplayContent = new\\(@\"(?<text>.*?)\"\\);", RegexOptions.Singleline);

        upstream.Success.Should().BeTrue();
        ScriptHelp.ArgumentsHelp.ReplaceLineEndings("\n").Should().Be(upstream.Groups["text"].Value.ReplaceLineEndings("\n"));
    }

    // The test's own source file is in the repository, also when the build output is not (Linux builds).
    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
