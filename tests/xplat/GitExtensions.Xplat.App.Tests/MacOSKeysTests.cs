using Avalonia.Input;
using NUnit.Framework;
using WinFormsKeys = System.Windows.Forms.Keys;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  The default keys of macOS and of the other OSes. They are checked on every OS, so CI on any of them finds a default that
///  macOS would never deliver.
/// </summary>
internal sealed class MacOSKeysTests
{
    [Test]
    public void No_default_on_macOS_should_be_a_key_macOS_keeps()
    {
        WinFormsKeys[] defaults =
        [
            .. Hotkeys.Browse.Defaults(macOS: true).Values,
            .. Hotkeys.Commit.Defaults(macOS: true).Values,
            .. Hotkeys.Grid.Defaults(macOS: true).Values,
            .. Hotkeys.Diff.Defaults(macOS: true).Values,
            .. Hotkeys.LeftPanel.Defaults(macOS: true).Values,
            .. Hotkeys.BrowseDiff.Defaults(macOS: true).Values,
            .. Hotkeys.Stash.Defaults(macOS: true).Values,
            .. Hotkeys.Conflicts.Defaults(macOS: true).Values,
        ];

        defaults.Intersect(Hotkeys.MacOSReservedKeys).Should().BeEmpty();
    }

    [Test]
    public void The_list_should_catch_the_keys_upstream_chose()
    {
        Hotkeys.MacOSReservedKeys.Should().Contain(Hotkeys.Browse.Defaults(macOS: false)[BrowseCommand.Commit]);
        Hotkeys.MacOSReservedKeys.Should().Contain(Hotkeys.Browse.Defaults(macOS: false)[BrowseCommand.Merge]);
        Hotkeys.MacOSReservedKeys.Should().Contain(Hotkeys.LeftPanel.Defaults(macOS: false)[LeftPanelCommand.MultiSelect]);
        Hotkeys.MacOSReservedKeys.Should().Contain(Hotkeys.Diff.Defaults(macOS: false)[DiffCommand.Replace]);
    }

    [Test]
    public void The_other_OSes_should_keep_upstreams_keys()
    {
        Hotkeys.Browse.Defaults(macOS: false)[BrowseCommand.Commit].Should().Be(WinFormsKeys.Control | WinFormsKeys.Space);
        Hotkeys.Browse.Defaults(macOS: false)[BrowseCommand.Merge].Should().Be(WinFormsKeys.Control | WinFormsKeys.M);
        Hotkeys.LeftPanel.Defaults(macOS: false)[LeftPanelCommand.MultiSelect].Should().Be(WinFormsKeys.Control | WinFormsKeys.Space);
    }

    [Test]
    public void A_macOS_key_should_not_take_another_commands_key()
    {
        WinFormsKeys commit = Hotkeys.Browse.Defaults(macOS: true)[BrowseCommand.Commit];
        WinFormsKeys merge = Hotkeys.Browse.Defaults(macOS: true)[BrowseCommand.Merge];

        Hotkeys.Browse.Defaults(macOS: true).Values.Should().ContainSingle(keys => keys == commit);
        Hotkeys.Browse.Defaults(macOS: true).Values.Should().ContainSingle(keys => keys == merge);
        Hotkeys.LeftPanel.Defaults(macOS: true).Values
            .Should().ContainSingle(keys => keys == Hotkeys.LeftPanel.Defaults(macOS: true)[LeftPanelCommand.MultiSelect]);

        // The browse window's keys also act while the grid has the focus, and the grid's own keys come first.
        Hotkeys.Grid.Defaults(macOS: true).Values.Should().NotContain([commit, merge]);
    }

    [Test]
    public void A_stored_hotkey_should_win_over_the_default_of_this_OS()
    {
        string stored = Hotkeys.Save(null,
            new Dictionary<BrowseCommand, KeyGesture?> { [BrowseCommand.Commit] = new KeyGesture(Key.K, Hotkeys.CommandModifier) });
        try
        {
            Hotkeys.Load(stored);

            Hotkeys.GestureFor(BrowseCommand.Commit).Should().Be(new KeyGesture(Key.K, Hotkeys.CommandModifier));
        }
        finally
        {
            Hotkeys.Load(null);
        }
    }

    [Test]
    [Platform(Include = "MacOsX")]
    public void The_keys_on_macOS_should_be_the_ones_it_leaves_free()
    {
        Hotkeys.Browse.DefaultGestureFor(BrowseCommand.Commit).Should().Be(new KeyGesture(Key.Return, KeyModifiers.Meta));
        Hotkeys.Browse.DefaultGestureFor(BrowseCommand.Merge).Should().Be(new KeyGesture(Key.M, KeyModifiers.Meta | KeyModifiers.Alt));
        Hotkeys.LeftPanel.DefaultGestureFor(LeftPanelCommand.MultiSelect).Should().Be(new KeyGesture(Key.Space, KeyModifiers.Alt));
        Hotkeys.Diff.DefaultGestureFor(DiffCommand.Replace).Should().Be(new KeyGesture(Key.F, KeyModifiers.Meta | KeyModifiers.Alt));
    }

    [Test]
    public void The_command_modifiers_name_should_match_what_a_gesture_shows()
    {
        new KeyGesture(Key.S, Hotkeys.CommandModifier).ToString().Should().Be($"{Hotkeys.CommandModifierName}+S");
    }
}
