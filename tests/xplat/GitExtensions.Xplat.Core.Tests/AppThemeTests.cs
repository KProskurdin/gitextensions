using System.Drawing;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Settings;
using GitExtUtils.GitUI.Theming;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// Reads upstream's own theme files from src/app/GitUI/Themes, so a change there shows up here.
internal sealed class AppThemeTests
{
    private string _userThemes = null!;
    private UpstreamThemeService _service = null!;

    [SetUp]
    public void Setup()
    {
        _userThemes = Path.Combine(Path.GetTempPath(), "xplat-themes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_userThemes);
        _service = new UpstreamThemeService(
            new AppThemePaths(Path.Combine(TestContext.CurrentContext.TestDirectory, "Themes"), _userThemes));
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_userThemes, recursive: true);

    [Test]
    public void GetThemeIds_should_list_the_system_mode_first_then_the_builtin_and_user_themes()
    {
        File.WriteAllText(Path.Combine(_userThemes, "mine.css"), "@import url(\"dark.css\");");

        IReadOnlyList<ThemeId> ids = _service.GetThemeIds();

        ids[0].Should().Be(ThemeId.WindowsAppColorModeId);
        ids.Should().Contain([
            ThemeId.DefaultLight, ThemeId.DefaultDark, new ThemeId("dark+", isBuiltin: true), new ThemeId("mine")
        ]);
    }

    [Test]
    public void Load_should_read_dark_as_a_dark_theme_with_its_graph_colors()
    {
        AppThemeColors colors = _service.Load(ThemeId.DefaultDark, [], systemIsDark: false);

        colors.IsDark.Should().BeTrue();
        colors.Error.Should().BeNull();
        colors.Get(AppColor.GraphBranch1).ToArgb().Should().Be(ColorTranslator.FromHtml("#db5b93").ToArgb());
        colors.Get(AppColor.GraphBranch8).IsEmpty.Should().BeTrue("dark.css leaves this lane color unused");
    }

    [Test]
    public void Load_should_follow_imports_and_apply_the_overrides()
    {
        AppThemeColors colors = _service.Load(new ThemeId("dark+", isBuiltin: true), [], systemIsDark: false);

        colors.Get(AppColor.Branch).ToArgb().Should().Be(ColorTranslator.FromHtml("#00d76b").ToArgb());
        colors.Get(AppColor.GraphBranch1).ToArgb().Should()
            .Be(ColorTranslator.FromHtml("#db5b93").ToArgb(), "dark+ imports dark");
    }

    [Test]
    public void Load_should_apply_the_colorblind_variation()
    {
        AppThemeColors colors = _service.Load(ThemeId.DefaultDark, [ThemeVariations.Colorblind], systemIsDark: false);

        colors.Get(AppColor.RemoteBranch).ToArgb().Should().Be(ColorTranslator.FromHtml("#0080ff").ToArgb());
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void Load_should_resolve_the_system_mode_by_the_operating_system(bool systemIsDark, bool expectedDark)
    {
        _service.Load(ThemeId.WindowsAppColorModeId, [], systemIsDark).IsDark.Should().Be(expectedDark);
    }

    [Test]
    public void Load_should_fall_back_to_the_default_theme_when_a_theme_cannot_be_read()
    {
        File.WriteAllText(Path.Combine(_userThemes, "broken.css"), ".NoSuchColor { color: #000000; }");

        AppThemeColors colors = _service.Load(new ThemeId("broken"), [], systemIsDark: true);

        colors.IsDark.Should().BeFalse();
        colors.Error.Should().Contain("user-defined theme broken").And.Contain("NoSuchColor");
    }
}
