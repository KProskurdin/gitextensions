using System.Drawing;
using AwesomeAssertions;
using GitCommands;
using GitExtensions.Xplat.Core.Avatars;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class AvatarTests
{
    private const string Email = "Test@Example.com";

    // md5("test@example.com"), as Gravatar hashes the trimmed lower-case address.
    private const string EmailMd5 = "55502f40dc8b7c769880b10874abc9d0";

    private string _cacheFolder = null!;

    [SetUp]
    public void Setup() => _cacheFolder = Path.Combine(Path.GetTempPath(), "xplat-avatar-tests-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_cacheFolder))
        {
            Directory.Delete(_cacheFolder, recursive: true);
        }
    }

    // The cases of upstream's InitialsAvatarGeneratorTests.
    [TestCase("albert.einstein@noreply.com", "albert einstein", "AE")]
    [TestCase("albert.einstein@noreply.com", "albert z middlename einstein (k)", "AE")]
    [TestCase("albert.einstein@noreply.com", "albert einstein (Nobel_price_winner)", "AE")]
    [TestCase("albert.einstein@noreply.com", "EINSTEIN, Albert middlename (Nobel_price_winner)", "EM")]
    [TestCase("albert.einstein@noreply.com", "albert", "Al")]
    [TestCase("albert.einstein@noreply.com", "", "AE")]
    [TestCase("albert.einstein@noreply.com", null, "AE")]
    [TestCase("albert.z.einstein@noreply.com", null, "AE")]
    [TestCase("albert_z_einstein@noreply.com", null, "AE")]
    [TestCase("albert.bose-einstein@noreply.com", null, "AE")]
    [TestCase("albert", "", "Al")]
    [TestCase("albert.einstein", "", "AE")]
    [TestCase("", "albert.einstein", "AE")]
    [TestCase("", "AlbertEinstein", "AE")]
    [TestCase("", "A-Einstein", "AE")]
    [TestCase("", "Alb-Einstein", "AE")]
    [TestCase("", "AEinstein", "AE")]
    [TestCase("1-pass@noreply.com", "1-pass", "1P")]
    [TestCase("", "1-pass", "1P")]
    [TestCase("1-pass@noreply.com", "", "1P")]
    [TestCase("/*+=/°¨", "'µ*", "'")]
    [TestCase("/*+=/°¨", "'µ *", "'*")]
    [TestCase("", "", "?")]
    public void AuthorInitials_should_be_upstreams(string email, string? name, string expected)
    {
        new AuthorInitials("SlateGray,RoyalBlue", 0.5f).For(email, name).Initials.Should().Be(expected);
    }

    [Test]
    public void AuthorInitials_should_pick_the_color_by_email_and_contrast_the_text()
    {
        AuthorInitials initials = new("White,Black", 0.5f);

        (string _, Color first, Color firstText) = initials.For("a@example.com", "A");
        (string _, Color again, Color _) = initials.For("a@example.com", "Another name");
        (string _, Color other, Color otherText) = initials.For("b@example.com", "B");

        again.Should().Be(first);
        other.Should().NotBe(first);
        firstText.Should().Be(first == Color.White ? Color.Black : Color.White);
        otherText.Should().Be(other == Color.White ? Color.Black : Color.White);
    }

    [Test]
    public void Candidates_should_be_only_the_initials_by_default()
    {
        AvatarService.Candidates(new AvatarOptions(), Email, "Test", 32).Should().Equal([null]);
    }

    [Test]
    public void Candidates_should_ask_github_then_gravatar_then_use_the_fallback()
    {
        IReadOnlyList<Uri?> initials = AvatarService.Candidates(new AvatarOptions(AvatarProvider.Default), Email, null, 32);

        initials.Select(uri => uri?.ToString()).Should().Equal(
            "https://avatars.githubusercontent.com/u/e?email=Test%40Example.com&s=32",
            $"https://www.gravatar.com/avatar/{EmailMd5}?r=g&d=404&s=32",
            null);

        IReadOnlyList<Uri?> identicon = AvatarService.Candidates(
            new AvatarOptions(AvatarProvider.Default, AvatarFallbackType.Identicon), Email, null, 32);

        identicon.Select(uri => uri?.ToString()).Skip(1).Should().Equal(
            $"https://www.gravatar.com/avatar/{EmailMd5}?r=g&d=identicon&s=32",
            $"https://www.gravatar.com/avatar/{EmailMd5}?r=g&d=identicon&f=y&s=32");
    }

    [TestCase("12345+octo-cat@users.noreply.github.com", "https://avatars.githubusercontent.com/octo-cat?s=40")]
    [TestCase("octocat@users.noreply.github.com", "https://avatars.githubusercontent.com/octocat?s=40")]
    [TestCase("dependabot[bot]@users.noreply.github.com", null)]
    public void GitHubUri_should_use_the_user_name_of_a_noreply_address(string email, string? expected)
    {
        (AvatarService.GitHubUri(email, 40)?.ToString()).Should().Be(expected);
    }

    [Test]
    public void Candidates_should_follow_the_custom_templates_and_upstreams_tags()
    {
        AvatarOptions options = new(AvatarProvider.Custom, AvatarFallbackType.Retro,
            "https://img.example/{md5}?s={imageSize}&n={name}&e={email}&x={unknown} ; <Default>; https://never.example");

        IReadOnlyList<Uri?> candidates = AvatarService.Candidates(options, Email, "A B", 64);

        candidates.Select(uri => uri?.ToString()).Should().Equal(
            $"https://img.example/{EmailMd5}?s=64&n=A+B&e=test%40example.com&x=",
            "https://avatars.githubusercontent.com/u/e?email=Test%40Example.com&s=64",
            $"https://www.gravatar.com/avatar/{EmailMd5}?r=g&d=404&s=64",
            null);
    }

    [Test]
    public void GetImage_should_take_the_first_image_and_keep_it_in_upstreams_cache_folder()
    {
        FakeDownloader downloader = new();
        byte[] image = [1, 2, 3];
        downloader.Images[AvatarService.GravatarUri(Email, 32, AvatarFallbackType.AuthorInitials, false)] = image;
        AvatarService service = new(() => new AvatarOptions(AvatarProvider.Default), _cacheFolder, downloader);

        service.GetImageAsync(Email, null, 32).GetAwaiter().GetResult().Should().Equal(image);

        downloader.Requests.Should().HaveCount(2);
        File.ReadAllBytes(Path.Combine(_cacheFolder, $"{Email}.32px.png")).Should().Equal(image);

        // Another run reads the cache instead of the network.
        FakeDownloader offline = new();
        new AvatarService(() => new AvatarOptions(AvatarProvider.Default), _cacheFolder, offline)
            .GetImageAsync(Email, null, 32).GetAwaiter().GetResult().Should().Equal(image);
        offline.Requests.Should().BeEmpty();
    }

    [Test]
    public void GetImage_should_skip_githubs_identicon_and_drop_an_expired_cache_file()
    {
        FakeDownloader downloader = new();
        byte[] identicon = [0x89, (byte)'P', (byte)'N', (byte)'G', 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0xA4, 0, 0, 1, 0xA4];
        downloader.Images[AvatarService.GitHubUri(Email, 32)!] = identicon;
        Directory.CreateDirectory(_cacheFolder);
        string cached = Path.Combine(_cacheFolder, $"{Email}.32px.png");
        File.WriteAllBytes(cached, [9]);
        File.SetLastWriteTime(cached, DateTime.Now.AddDays(-20));
        AvatarService service = new(() => new AvatarOptions(AvatarProvider.Default, CacheDays: 13), _cacheFolder,
            downloader);

        service.GetImageAsync(Email, null, 32).GetAwaiter().GetResult().Should().BeNull();

        File.Exists(cached).Should().BeFalse();
        downloader.Requests.Should().HaveCount(2);
    }

    [Test]
    public void ClearCache_should_delete_the_files_and_tell_the_views()
    {
        Directory.CreateDirectory(_cacheFolder);
        File.WriteAllBytes(Path.Combine(_cacheFolder, "a@example.com.32px.png"), [1]);
        AvatarService service = new(() => new AvatarOptions(), _cacheFolder, new FakeDownloader());
        bool changed = false;
        service.AvatarsChanged += (_, _) => changed = true;

        service.ClearCacheAsync().GetAwaiter().GetResult();

        Directory.GetFiles(_cacheFolder).Should().BeEmpty();
        changed.Should().BeTrue();
    }

    private sealed class FakeDownloader : IAvatarDownloader
    {
        public Dictionary<Uri, byte[]> Images { get; } = [];

        public List<Uri> Requests { get; } = [];

        public Task<byte[]?> DownloadAsync(Uri uri)
        {
            Requests.Add(uri);
            return Task.FromResult(Images.TryGetValue(uri, out byte[]? image) ? image : null);
        }
    }
}
