using AwesomeAssertions;
using GitCommands;
using NUnit.Framework;

namespace GitCommandsTests;

internal sealed class XplatEmulatedRegistryStoreTests
{
    private string _directory = null!;

    [SetUp]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), "xplat-store-" + Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void Set_should_be_readable_by_a_new_store_on_the_same_directory()
    {
        new EmulatedRegistryStore(_directory).Set("GitCommand", "/usr/bin/git");

        new EmulatedRegistryStore(_directory).TryGet("GitCommand", out string? value).Should().BeTrue();
        value.Should().Be("/usr/bin/git");
    }

    [Test]
    public void TryGet_should_return_false_for_unknown_name()
    {
        new EmulatedRegistryStore(_directory).TryGet("Missing", out _).Should().BeFalse();
    }

    [Test]
    public void Set_should_overwrite_existing_value()
    {
        EmulatedRegistryStore store = new(_directory);
        store.Set("SshPath", "/old");
        store.Set("SshPath", "/new");

        new EmulatedRegistryStore(_directory).TryGet("SshPath", out string? value).Should().BeTrue();
        value.Should().Be("/new");
    }

    [Test]
    public void GetNames_should_list_all_stored_names()
    {
        EmulatedRegistryStore store = new(_directory);
        store.Set("A", "1");
        store.Set("B", "2");

        new EmulatedRegistryStore(_directory).GetNames().Should().BeEquivalentTo(["A", "B"]);
    }

    [Test]
    public void TryGet_should_start_empty_and_keep_unreadable_file_aside()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "GitExtensions.registry.json");
        File.WriteAllText(path, "{ not json");

        EmulatedRegistryStore store = new(_directory);

        store.TryGet("Anything", out _).Should().BeFalse();
        File.Exists(path + ".corrupt").Should().BeTrue();
    }
}
