using System.Reflection;
using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.Plugins;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class PluginTests
{
    private static readonly Guid _pluginId = new("5D8B2F3A-0C1E-4E59-9B7A-2F0C6B1D9E44");

    [Test]
    public void Load_should_stand_in_for_a_plugin_that_fails_and_order_the_plugins_by_name()
    {
        IReadOnlyList<IGitPlugin> plugins = PluginLoader.Load(
        [
            new Lazy<IGitPlugin>(() => new SamplePlugin("Zeta")),
            new Lazy<IGitPlugin>(() => throw new InvalidOperationException("broken plugin")),
            new Lazy<IGitPlugin>(() => new SamplePlugin("alpha")),
        ]);

        plugins.Select(plugin => plugin.Name).Should().Equal("alpha", FailedPlugin.FailedToLoadPlugin, "Zeta");
        plugins[1].Should().BeOfType<FailedPlugin>().Which.Error.Should().Contain("broken plugin");
    }

    [Test]
    public void SkippedPlugins_should_say_that_a_plugin_needing_windows_forms_is_built_for_windows()
    {
        ReflectionTypeLoadException exception = new([null], [
            new FileNotFoundException("Could not load file or assembly",
                "System.Windows.Forms, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
        ]);

        SkippedPlugins.Reason(exception).Should().Be(
            "Built for Git Extensions for Windows: it uses Windows Forms, which this version does not have.");
    }

    [Test]
    public void SkippedPlugins_should_name_the_missing_assemblies()
    {
        ReflectionTypeLoadException exception = new([null], [
            new FileNotFoundException("missing", "GitExtensions.Extensibility, Version=0.3.0.0, Culture=neutral"),
            new FileNotFoundException("missing", "GitExtensions.Extensibility, Version=0.3.0.0, Culture=neutral"),
        ]);

        SkippedPlugins.Reason(exception).Should()
            .Be("It needs GitExtensions.Extensibility, which this version does not have.");
    }

    [Test]
    public void SkippedPlugins_should_list_a_file_that_is_not_an_assembly()
    {
        string file = Path.Combine(Path.GetTempPath(), $"GitExtensions.Broken.{Guid.NewGuid():N}.dll");
        File.WriteAllText(file, "not an assembly");
        try
        {
            IReadOnlyList<SkippedPlugin> skipped = SkippedPlugins.Find([
                new FileInfo(file),
                new FileInfo(typeof(PluginTests).Assembly.Location)
            ]);

            skipped.Should().ContainSingle().Which.Path.Should().Be(file);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Test]
    public void Load_should_store_settings_under_upstreams_plugin_keys()
    {
        SamplePlugin plugin = new("Sample");
        PluginLoader.Load([new Lazy<IGitPlugin>(() => plugin)]);
        InMemorySettingsSource settings = new();
        settings.SetValue("SampleOld setting", "old");
        plugin.SettingsContainer!.SetSettingsSource(settings);

        string? oldValue = plugin.Settings.GetValue("Old setting");
        plugin.Settings.SetValue("Arguments", "fetch --prune");

        // As upstream's GitPluginSettingsContainer: "<id>.<name>", falling back to the older "<description><name>".
        oldValue.Should().Be("old");
        settings.Values.Should().Contain($"{_pluginId}.Arguments", "fetch --prune");
    }

    [Test]
    public void Editor_should_show_each_setting_type_with_upstreams_editor()
    {
        PluginSettingsEditor editor = new(new SamplePlugin("Sample"), new InMemorySettingsSource());

        editor.Rows.Select(row => (row.Kind, row.Caption)).Should().Equal(
            (PluginSettingKind.Text, "Arguments"),
            (PluginSettingKind.Number, "Interval"),
            (PluginSettingKind.Bool, "Enabled"),
            (PluginSettingKind.Choice, "Mode"),
            (PluginSettingKind.Password, "Token"),
            (PluginSettingKind.Note, "Read me"));
        editor.Rows[3].Choices.Should().Equal("fast", "slow");
        editor.Rows[5].Text.Should().Be("Just a note.");
        editor.Rows[0].Placeholder.Should().Be(PluginSettingRow.StringPlaceholder);
    }

    [Test]
    public void Editor_at_a_single_level_should_show_unset_values_as_unset_and_write_what_was_entered()
    {
        InMemorySettingsSource settings = new();
        PluginSettingsEditor editor = new(new SamplePlugin("Sample"), settings);

        editor.Rows[0].Text.Should().BeNull();
        editor.Rows[2].Checked.Should().BeNull();
        editor.Rows[0].Text = "  " + PluginSettingRow.EmptyStringValue + " ";
        editor.Rows[1].Text = "30";
        editor.Rows[2].Checked = false;
        editor.Rows[3].Text = "slow";
        editor.Save();

        settings.Values.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            [$"{_pluginId}.Arguments"] = "",
            [$"{_pluginId}.Interval"] = "30",
            [$"{_pluginId}.Enabled"] = "false",
            [$"{_pluginId}.Mode"] = "slow",
        });
        editor.Rows[0].Text.Should().Be(PluginSettingRow.EmptyStringValue);
        new PluginSettingsEditor(new SamplePlugin("Sample"), settings).Rows[0].Text
            .Should().Be(PluginSettingRow.EmptyStringValue);
    }

    [Test]
    public void Editor_at_the_effective_level_should_show_defaults_and_not_write_unchanged_values()
    {
        InMemorySettingsSource settings = new(SettingLevel.Effective);
        PluginSettingsEditor editor = new(new SamplePlugin("Sample"), settings);

        editor.Rows.Take(4).Select(row => row.Kind == PluginSettingKind.Bool ? row.Checked?.ToString() : row.Text)
            .Should().Equal("fetch --all", "0", "True", "fast");
        editor.Save();
        settings.Values.Should().BeEmpty();

        editor.Rows[2].Checked = false;
        editor.Save();
        settings.Values.Should().Equal(new Dictionary<string, string> { [$"{_pluginId}.Enabled"] = "false" });
    }

    [Test]
    public void Number_rows_should_reject_text_that_is_not_a_number_and_store_nothing_for_it()
    {
        InMemorySettingsSource settings = new();
        settings.SetValue($"{_pluginId}.Interval", "10");
        PluginSettingsEditor editor = new(new SamplePlugin("Sample"), settings);
        PluginSettingRow interval = editor.Rows[1];

        interval.Text.Should().Be("10");
        interval.Text = "ten";
        interval.IsValid.Should().BeFalse();
        editor.Save();

        settings.Values.Should().NotContainKey($"{_pluginId}.Interval");
        interval.Text = "";
        interval.IsValid.Should().BeTrue();
    }

    [Test]
    public void Settings_with_a_WinForms_editor_should_be_unsupported()
    {
        PluginSettingRow row = PluginSettingRow.Create(new CustomEditorSetting());

        row.Kind.Should().Be(PluginSettingKind.Unsupported);
        row.Caption.Should().Be("Custom");
    }

    [Test]
    public void A_plugin_without_settings_should_have_no_rows()
    {
        PluginSettingsEditor editor = new(new SamplePlugin("Sample", hasSettings: false), new InMemorySettingsSource());

        editor.Rows.Should().BeEmpty();
        editor.Title.Should().Be("Sample");
    }

    [Test]
    public void A_credentials_setting_is_edited_where_upstream_edits_it()
    {
        CredentialsPlugin plugin = new();

        // Only reads: the repository's shared level has no credentials, and elsewhere a new name has none stored.
        PluginSettingRow row = RowAt(SettingLevel.Distributed);
        row.Kind.Should().Be(PluginSettingKind.Credentials);
        row.IsEditable.Should().BeFalse();
        if (!OperatingSystem.IsWindows())
        {
            row = RowAt(SettingLevel.Global);
            row.IsEditable.Should().BeFalse();
            row.NotEditableReason.Should().Be(PluginSettingRow.CredentialsNeedWindows);
            return;
        }

        row = RowAt(SettingLevel.Global);
        row.IsEditable.Should().BeTrue();
        row.Text.Should().BeEmpty();
        return;

        PluginSettingRow RowAt(SettingLevel level)
            => new PluginSettingsEditor(plugin, new InMemorySettingsSource(level)).Rows[0];
    }

    private sealed class CredentialsPlugin : GitPluginBase
    {
        private readonly CredentialsSetting _login = new($"xplat-test-{Guid.NewGuid():N}", "Login", () => "/work/repo");

        public CredentialsPlugin()
            : base(hasSettings: true)
        {
            Id = Guid.NewGuid();
            Name = "Credentials";
            Description = Name;
        }

        public override IEnumerable<ISetting> GetSettings() => [_login];

        public override bool Execute(GitUIEventArgs args) => false;
    }

    [Test]
    public void The_in_memory_store_keeps_each_repository_level_apart()
    {
        InMemorySettingsSource global = new();
        InMemoryPluginSettingsStore store = new(global);

        store.Open("/repo", SettingLevel.Effective).Should().BeSameAs(global);
        store.Open(null, SettingLevel.Local).Should().BeSameAs(global);
        store.Open("/repo", SettingLevel.Local).SettingLevel.Should().Be(SettingLevel.Local);
        store.Open("/repo", SettingLevel.Local).Should().BeSameAs(store.Open("/repo", SettingLevel.Local));
    }

    private sealed class SamplePlugin : GitPluginBase
    {
        private readonly StringSetting _arguments = new("Arguments", "fetch --all");
        private readonly NumberSetting<int> _interval = new("Interval", 0);
        private readonly BoolSetting _enabled = new("Enabled", true);
        private readonly ChoiceSetting _mode = new("Mode", ["fast", "slow"]);
        private readonly PasswordSetting _token = new("Token", "");
        private readonly PseudoSetting _note = new("Just a note.", "Read me");

        public SamplePlugin(string name, bool hasSettings = true)
            : base(hasSettings)
        {
            Id = _pluginId;
            Name = name;
            Description = name;
        }

        public override IEnumerable<ISetting> GetSettings() => [_arguments, _interval, _enabled, _mode, _token, _note];

        public override bool Execute(GitUIEventArgs args) => false;
    }

    private sealed class CustomEditorSetting : ISetting, ISettingControlBinding
    {
        public string Name => "Custom";

        public string Caption => "Custom";

        public ISettingControlBinding CreateControlBinding() => this;

        public System.Windows.Forms.Control GetControl() => new();

        public void LoadSetting(SettingsSource settings)
        {
        }

        public void SaveSetting(SettingsSource settings)
        {
        }

        string ISettingControlBinding.Caption() => Caption;

        public ISetting GetSetting() => this;
    }
}
