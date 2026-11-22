using AwesomeAssertions;
using GitExtensions.Xplat.Core.Platform;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class ShellExtensionFilesTests
{
    private const string Installed = "installed";
    private const string App = "app";

    private static readonly string _installed32 = Path.Join(Installed, ShellExtensionFiles.ShellEx32Name);
    private static readonly string _installed64 = Path.Join(Installed, ShellExtensionFiles.ShellEx64Name);
    private static readonly string _app32 = Path.Join(App, ShellExtensionFiles.ShellEx32Name);
    private static readonly string _app64 = Path.Join(App, ShellExtensionFiles.ShellEx64Name);

    [Test]
    public void Find_should_look_in_the_install_folder_first_then_the_apps()
    {
        ShellExtensionFiles.Find(ShellExtensionFiles.ShellEx32Name, Installed, App, Files(_installed32, _app32))
            .Should().Be(_installed32);
        ShellExtensionFiles.Find(ShellExtensionFiles.ShellEx32Name, Installed, App, Files(_app32)).Should().Be(_app32);
        ShellExtensionFiles.Find(ShellExtensionFiles.ShellEx32Name, null, App, Files(_installed32)).Should().BeNull();
    }

    [Test]
    public void Exist_should_need_the_64_bit_file_on_a_64_bit_windows_only()
    {
        ShellExtensionFiles.Exist(Installed, App, is64BitWindows: true, Files(_installed32)).Should().BeFalse();
        ShellExtensionFiles.Exist(Installed, App, is64BitWindows: false, Files(_installed32)).Should().BeTrue();
        ShellExtensionFiles.Exist(Installed, App, is64BitWindows: true, Files(_installed32, _app64)).Should().BeTrue();
    }

    [Test]
    public void ToRegister_should_take_the_apps_files_and_folder_when_it_has_them()
    {
        (IReadOnlyList<string> files, bool setInstallDir) = ShellExtensionFiles.ToRegister(Installed, App,
            is64BitWindows: true, Files(_installed32, _installed64, _app32, _app64));

        files.Should().Equal(_app64, _app32);
        setInstallDir.Should().BeTrue();
    }

    [Test]
    public void ToRegister_should_keep_the_install_folder_when_only_it_has_the_files()
    {
        (IReadOnlyList<string> files, bool setInstallDir) = ShellExtensionFiles.ToRegister(Installed, App,
            is64BitWindows: true, Files(_installed32, _installed64, _app32));

        files.Should().Equal(_installed64, _installed32);
        setInstallDir.Should().BeFalse();
    }

    [Test]
    public void ToRegister_should_fail_without_a_file()
    {
        Action register = () => ShellExtensionFiles.ToRegister(Installed, App, is64BitWindows: true, Files(_installed32));

        register.Should().Throw<FileNotFoundException>().Which.FileName.Should().Be(ShellExtensionFiles.ShellEx64Name);
    }

    private static Func<string, bool> Files(params string[] files) => files.Contains;
}
