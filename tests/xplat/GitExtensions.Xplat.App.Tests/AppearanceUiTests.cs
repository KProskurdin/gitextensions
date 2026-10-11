using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitCommands;
using GitExtensions.Xplat.Core.Avatars;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class AppearanceUiTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    // A 1x1 PNG, as an avatar provider sends it.
    private static readonly byte[] _png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        ResetPreferences();
    }

    [TearDown]
    public void TearDown()
    {
        ResetPreferences();
        AppServices.Avatars.Reload();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_grid_and_the_details_show_the_authors_initials_and_then_a_custom_providers_image()
    {
        MainWindow window = Open();
        Find<ListBox>(window, "CommitList").SelectedIndex = 0;
        AvatarView details = Find<AvatarView>(window, "DetailAvatar");
        WaitUntil(() => details.IsVisible && details.InitialsText == "Te");
        details.HasImage.Should().BeFalse();
        WaitUntil(() => GridAvatars(window).Any());
        GridAvatars(window).Should().OnlyContain(avatar => avatar.IsEffectivelyVisible && avatar.InitialsText == "Te");

        // As upstream's custom provider: the template's address, then the image replaces the initials.
        TestAppBuilder.AvatarDownloads.Offer(new Uri("https://img.example/test%40example.com"), _png);
        TestAppBuilder.Preferences.Avatars = new AvatarOptions(AvatarProvider.Custom, AvatarFallbackType.AuthorInitials,
            "https://img.example/{email}");
        AppServices.Avatars.Reload();
        WaitUntil(() => details.HasImage && GridAvatars(window).All(avatar => avatar.HasImage));
        window.Close();
    }

    [AvaloniaTest]
    public void The_author_images_settings_are_saved_and_apply_at_once()
    {
        MainWindow window = Open();
        Find<ListBox>(window, "CommitList").SelectedIndex = 0;
        WaitUntil(() => Find<AvatarView>(window, "DetailAvatar").IsVisible);

        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);
        Find<ComboBox>(settings, "AvatarProviderBox").SelectedItem.Should().Be(new AvatarProviderOption(AvatarProvider.None));
        Find<ComboBox>(settings, "AvatarFallbackBox").SelectedItem?.ToString().Should().Be("Author initials");
        Find<TextBox>(settings, "CustomAvatarTemplateBox").IsEnabled.Should().BeFalse();

        Find<ComboBox>(settings, "AvatarProviderBox").SelectedItem = new AvatarProviderOption(AvatarProvider.Custom);
        Find<TextBox>(settings, "CustomAvatarTemplateBox").IsEnabled.Should().BeTrue();
        Find<TextBox>(settings, "CustomAvatarTemplateBox").Text = "https://img.example/{md5}";
        Find<ComboBox>(settings, "AvatarFallbackBox").SelectedItem = new AvatarFallbackOption(AvatarFallbackType.Retro);
        Find<NumericUpDown>(settings, "AvatarCacheDaysBox").Value = 3;
        Find<CheckBox>(settings, "ShowAvatarInCommitInfoCheck").IsChecked = false;
        Find<CheckBox>(settings, "ShowAvatarColumnCheck").IsChecked = false;
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        TestAppBuilder.Preferences.Avatars.Should().Be(new AvatarOptions(AvatarProvider.Custom, AvatarFallbackType.Retro,
            "https://img.example/{md5}", 3));
        TestAppBuilder.Preferences.ShowAuthorAvatarInCommitInfo.Should().BeFalse();
        WaitUntil(() => !Find<AvatarView>(window, "DetailAvatar").IsVisible);
        WaitUntil(() => GridAvatars(window).All(avatar => !avatar.IsEffectivelyVisible));
        window.Close();
    }

    [AvaloniaTest]
    public void File_lists_follow_the_truncation_setting_and_the_dashboard_shows_branches()
    {
        Directory.CreateDirectory(Path.Combine(_repo.Path, "deep", "folder"));
        File.WriteAllText(Path.Combine(_repo.Path, "deep", "folder", "file.txt"), "x");
        _repo.Run("add", ".");
        MainWindow window = Open();

        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);
        Find<ComboBox>(settings, "TruncatePathBox").SelectedItem?.ToString().Should().Be("None");
        Find<CheckBox>(settings, "ShowRepoBranchCheck").IsChecked.Should().BeTrue();
        Find<ComboBox>(settings, "TruncatePathBox").SelectedItem =
            TruncatePathOption.All.Single(option => option.Method == TruncatePathMethod.FileNameOnly);
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());
        WaitUntil(() => TestAppBuilder.Preferences.TruncatePathMethod == TruncatePathMethod.FileNameOnly);

        WaitUntil(() => Find<Button>(window, "CommitDialogButton").IsEnabled);
        Click(window, "CommitDialogButton");
        WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
        CommitWindow commit = window.OwnedWindows.OfType<CommitWindow>().Single();
        WaitUntil(() => Find<ListBox>(commit, "StagedList").GetVisualDescendants().OfType<TextBlock>()
            .Any(text => text.Text == "file.txt"));
        commit.Close();

        TestAppBuilder.Preferences.TruncatePathMethod = TruncatePathMethod.TrimStart;
        FilePathTrimming.Apply(TestAppBuilder.Preferences);
        Avalonia.Application.Current!.Resources[FilePathTrimming.ResourceKey].Should()
            .Be(Avalonia.Media.TextTrimming.LeadingCharacterEllipsis);
        window.Close();
    }

    [AvaloniaTest]
    public void A_themes_system_colors_set_fluents_text_and_accent()
    {
        IAppThemeService themes = AppServices.Themes;
        AppThemeColors light = themes.Load(GitExtUtils.GitUI.Theming.ThemeId.DefaultLight, [], systemIsDark: false);
        AppServices.Themes = new StubThemeService(themes, light with
        {
            SystemColors = new Dictionary<System.Drawing.KnownColor, System.Drawing.Color>
            {
                [System.Drawing.KnownColor.WindowText] = System.Drawing.Color.FromArgb(0x10, 0x20, 0x30),
                [System.Drawing.KnownColor.Highlight] = System.Drawing.Color.FromArgb(0x28, 0x64, 0xb4),
            },
        });
        object? accent = Avalonia.Application.Current!.Resources["SystemAccentColor"];
        MainWindow window = Open();
        try
        {
            SaveSettings(window);
            WaitUntil(() => Equals(Avalonia.Application.Current!.Resources["SystemAccentColor"],
                Avalonia.Media.Color.Parse("#2864b4")));
            Avalonia.Application.Current!.Resources["SystemControlForegroundBaseHighBrush"].Should()
                .BeAssignableTo<Avalonia.Media.ISolidColorBrush>().Which.Color.Should()
                .Be(Avalonia.Media.Color.Parse("#102030"));
        }
        finally
        {
            AppServices.Themes = themes;
            SaveSettings(window);
            WaitUntil(() => Equals(Avalonia.Application.Current!.Resources["SystemAccentColor"], accent));
            window.Close();
        }
    }

    private static void SaveSettings(MainWindow window)
    {
        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());
    }

    private sealed class StubThemeService(IAppThemeService inner, AppThemeColors colors) : IAppThemeService
    {
        public IReadOnlyList<GitExtUtils.GitUI.Theming.ThemeId> GetThemeIds() => inner.GetThemeIds();

        public AppThemeColors Load(GitExtUtils.GitUI.Theming.ThemeId id, IReadOnlyList<string> variations, bool systemIsDark) => colors;
    }

    private static void ResetPreferences()
    {
        TestAppBuilder.Preferences.Avatars = new AvatarOptions();
        TestAppBuilder.Preferences.ShowAuthorAvatarColumn = true;
        TestAppBuilder.Preferences.ShowAuthorAvatarInCommitInfo = true;
        TestAppBuilder.Preferences.TruncatePathMethod = TruncatePathMethod.None;
        FilePathTrimming.Apply(TestAppBuilder.Preferences);
    }

    private static IEnumerable<AvatarView> GridAvatars(MainWindow window)
        => Find<ListBox>(window, "CommitList").GetVisualDescendants().OfType<AvatarView>();

    private MainWindow Open()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        Find<TextBox>(window, "PathBox").Text = _repo.Path;
        Click(window, "OpenButton");
        WaitUntil(() => Find<Button>(window, "OpenButton").IsEnabled && Find<ListBox>(window, "CommitList").ItemCount > 0);
        return window;
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        RoutedEvent click = control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent;
        control.RaiseEvent(new RoutedEventArgs(click));
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
                Assert.Fail("Timed out waiting for the UI");
            }

            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
    }
}
