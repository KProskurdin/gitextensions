using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class VerifyDatabaseTests
{
    private const string Hash = "0123456789abcdef0123456789abcdef01234567";

    [TestCase(false, false, false, "")]
    [TestCase(true, false, false, " --unreachable")]
    [TestCase(false, true, false, " --full")]
    [TestCase(false, false, true, " --no-reflogs")]
    [TestCase(true, true, true, " --unreachable --full --no-reflogs")]
    public void Options_should_be_upstreams_fsck_options(bool unreachable, bool fullCheck, bool noReflogs, string expected)
        => VerifyDatabase.Options(unreachable, fullCheck, noReflogs).Should().Be(expected);

    [Test]
    public void SaveObjectsArguments_should_save_with_the_same_options()
        => VerifyDatabase.SaveObjectsArguments(" --no-reflogs").Should().Be("fsck-objects --lost-found --no-reflogs");

    [TestCase("{\"name\": 1}", "json")]
    [TestCase("{\\rtf1 text", "rtf")]
    [TestCase("<?xml version=\"1.0\"?>", "xml")]
    [TestCase("<Project Sdk=\"x\">", "xml")]
    [TestCase("<!DOCTYPE html>", "html")]
    [TestCase("#!/bin/sh", "sh")]
    [TestCase("# Title", "md")]
    [TestCase("import * as x from 'y'", "js")]
    [TestCase("import java.util.List;", "java")]
    [TestCase("plain words", "txt")]
    [TestCase("", "txt")]
    public void GuessFileType_should_take_the_first_matching_start(string content, string expected)
        => VerifyDatabase.GuessFileType(content).Should().Be(expected);

    [Test]
    public void GuessFileName_should_name_the_blob_after_its_hash_and_type()
        => VerifyDatabase.GuessFileName("{}", Hash).Should().Be($"LOST_FOUND_{Hash}.json");

    [Test]
    public void WithSeemingType_should_add_the_guessed_type_to_the_diagnostics()
        => VerifyDatabase.WithSeemingType("dangling blob", "#!/bin/sh").Should().Be("dangling blob (seemingly: sh)");

    [Test]
    public void SaveAsExtensions_should_offer_the_extension_and_its_equivalents()
    {
        VerifyDatabase.SaveAsExtensions("LOST_FOUND_x.js").Should().Equal("js", "ts", "jsx", "tsx");
        VerifyDatabase.SaveAsExtensions("LOST_FOUND_x.png").Should().Equal("png");
    }
}
