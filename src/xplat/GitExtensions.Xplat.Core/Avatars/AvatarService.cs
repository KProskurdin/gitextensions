using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using GitCommands;
using GitUI.Avatars;

namespace GitExtensions.Xplat.Core.Avatars;

/// <summary>
///  Upstream's avatar settings (Settings > Appearance > Author images): <c>Appearance.AvatarProvider</c>,
///  <c>GravatarDefaultImageType</c>, <c>CustomAvatarTemplate</c> and <c>authorimagecachedays</c>.
/// </summary>
public sealed record AvatarOptions(
    AvatarProvider Provider = AvatarProvider.None,
    AvatarFallbackType Fallback = AvatarFallbackType.AuthorInitials,
    string CustomTemplate = "",
    int CacheDays = 13);

/// <summary>
///  Downloads an avatar image; null when there is none or the network fails.
/// </summary>
public interface IAvatarDownloader
{
    Task<byte[]?> DownloadAsync(Uri uri);
}

/// <summary>
///  Upstream's <c>AvatarService</c> chain without its WinForms images: the providers upstream's settings choose (GitHub and
///  Gravatar, the user's templates, Gravatar's generated images) give image files, kept in upstream's cache folder
///  (<c>AppSettings.AvatarImageCachePath</c>, "<c>{email}.{size}px.png</c>", so both apps share it). Null means the author's
///  initials are shown, upstream's local fallback, which the view draws itself.
/// </summary>
public sealed partial class AvatarService
{
    // Upstream's SafetynetAvatarProvider limits.
    private const int UpperSizeLimit = 512;
    private const int DefaultSize = 64;

    // Upstream's FileSystemAvatarCache: a cache days setting below one means 30 days.
    private const int DefaultCacheDays = 30;

    // GitHub returns its identicon, always 420 pixels wide, for an author without an avatar (upstream's
    // GithubAvatarProvider); it is treated as no avatar so the chosen fallback shows.
    private const int GitHubIdenticonSize = 420;

    private readonly Func<AvatarOptions> _options;
    private readonly string _cacheFolder;
    private readonly IAvatarDownloader _downloader;
    private readonly int _memoryCacheSize;
    private readonly ConcurrentDictionary<(string Email, int Size), Task<byte[]?>> _memory = new();

    /// <param name="options">Read at each request, so a changed setting applies to the next avatar.</param>
    /// <param name="cacheFolder">Upstream's <c>AppSettings.AvatarImageCachePath</c>.</param>
    /// <param name="memoryCacheSize">Upstream's <c>Appearance.AvatarCacheSize</c>.</param>
    public AvatarService(Func<AvatarOptions> options, string cacheFolder, IAvatarDownloader downloader,
        int memoryCacheSize = 200)
    {
        _options = options;
        _cacheFolder = cacheFolder;
        _downloader = downloader;
        _memoryCacheSize = Math.Max(1, memoryCacheSize);
    }

    /// <summary>
    ///  Raised when the cache was cleared or the settings changed, so views read their avatars again.
    /// </summary>
    public event EventHandler? AvatarsChanged;

    [GeneratedRegex(@"^(\d+\+)?(?<username>[^@]+)@users\.noreply\.github\.com$",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex GitHubEmailRegex { get; }

    /// <summary>
    ///  The avatar image file (PNG, JPEG, ... as the provider sent it) of the author, or null for the author's initials.
    /// </summary>
    public Task<byte[]?> GetImageAsync(string email, string? name, int size)
    {
        size = size < 1 ? DefaultSize : Math.Min(size, UpperSizeLimit);
        IReadOnlyList<Uri?> candidates = Candidates(_options(), email, name, size);
        if (candidates is [null, ..])
        {
            return Task.FromResult<byte[]?>(null);
        }

        if (_memory.Count >= _memoryCacheSize)
        {
            _memory.Clear();
        }

        return _memory.GetOrAdd((email, size), _ => LoadAsync(email, size, candidates));
    }

    /// <summary>
    ///  Forgets the avatars read in this run, after the avatar settings changed (upstream's
    ///  <c>AvatarService.UpdateAvatarProvider</c>); the cache files stay.
    /// </summary>
    public void Reload()
    {
        _memory.Clear();
        AvatarsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///  Upstream's "Clear image cache": the files in the cache folder and the images read in this run.
    /// </summary>
    public Task ClearCacheAsync()
        => Task.Run(() =>
        {
            _memory.Clear();
            if (Directory.Exists(_cacheFolder))
            {
                foreach (string file in Directory.GetFiles(_cacheFolder))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        Trace.WriteLine($"Failed to delete file '{file}'. Error: {ex}");
                    }
                }
            }

            AvatarsChanged?.Invoke(this, EventArgs.Empty);
        });

    /// <summary>
    ///  The images to try in upstream's order for the settings; a null entry stands for the author's initials, which end the
    ///  list. Built as upstream's <c>AvatarService.CreateAvatarProvider</c>: the main provider, then the fallback.
    /// </summary>
    public static IReadOnlyList<Uri?> Candidates(AvatarOptions options, string email, string? name, int size)
    {
        List<Uri?> candidates = [];
        switch (options.Provider)
        {
            case AvatarProvider.Default:
                AddDefault(options.Fallback);
                break;
            case AvatarProvider.Custom:
                AddCustom();
                break;
        }

        // Upstream's BuildFallbackProvider: Gravatar's generated images, otherwise the author's initials.
        candidates.Add(GravatarFallback(options.Fallback) is not null
            ? GravatarUri(email, size, options.Fallback, forceFallback: true)
            : null);

        int initials = candidates.IndexOf(null);
        return initials < 0 ? candidates : candidates[..(initials + 1)];

        // GitHub, then Gravatar with the fallback image Gravatar makes itself (upstream's BuildDefaultMainProvider).
        void AddDefault(AvatarFallbackType? fallback)
        {
            if (GitHubUri(email, size) is { } github)
            {
                candidates.Add(github);
            }

            candidates.Add(GravatarUri(email, size, fallback, forceFallback: false));
        }

        // Upstream's CustomAvatarProvider: ';'-separated URL templates and "<Default>" or "<None>" for upstream's own
        // providers, which end in the initials as upstream creates them without a fallback type.
        void AddCustom()
        {
            foreach (string segment in options.CustomTemplate.Split(';', StringSplitOptions.TrimEntries))
            {
                if (segment.StartsWith('<') && segment.EndsWith('>'))
                {
                    if (Enum.TryParse(segment[1..^1], ignoreCase: true, out AvatarProvider provider)
                        && provider != AvatarProvider.Custom)
                    {
                        if (provider == AvatarProvider.Default)
                        {
                            AddDefault(fallback: null);
                        }

                        candidates.Add(null);
                    }
                }
                else if (segment.Length > 0
                         && ResolveTemplate(segment, email, name, size) is { Length: > 0 } uri
                         && Uri.TryCreate(uri, UriKind.Absolute, out Uri? resolved))
                {
                    candidates.Add(resolved);
                }
            }
        }
    }

    /// <summary>
    ///  Upstream's <c>GravatarProvider</c> address: the MD5 of the trimmed lower-case e-mail, rating "g", and the fallback
    ///  image Gravatar makes (or 404, so the next provider is asked).
    /// </summary>
    public static Uri GravatarUri(string email, int size, AvatarFallbackType? fallback, bool forceFallback)
    {
        string query = $"r=g&d={GravatarFallback(fallback) ?? "404"}{(forceFallback ? "&f=y" : "")}&s={size}";
        return new UriBuilder("https", "www.gravatar.com") { Path = $"/avatar/{Hash(MD5.Create(), email)}", Query = query }
            .Uri;
    }

    /// <summary>
    ///  Upstream's <c>GithubAvatarProvider</c> address: the user's avatar for a users.noreply.github.com address, otherwise
    ///  GitHub's look-up by e-mail. Null for a bot's address, whose avatar upstream finds through the GitHub API.
    /// </summary>
    public static Uri? GitHubUri(string email, int size)
    {
        Match match = GitHubEmailRegex.Match(email);
        if (!match.Success)
        {
            return new Uri($"https://avatars.githubusercontent.com/u/e?email={HttpUtility.UrlEncode(email)}&s={size}");
        }

        string username = match.Groups["username"].Value;
        return username.Contains('[')
            ? null
            : new Uri($"https://avatars.githubusercontent.com/{HttpUtility.UrlEncode(username)}?s={size}");
    }

    /// <summary>
    ///  Upstream's custom template variables: {name}, {email} (trimmed, lower case), {md5}, {sha1}, {sha256} of the e-mail
    ///  and {imageSize}; an unknown variable is left empty.
    /// </summary>
    public static string ResolveTemplate(string template, string email, string? name, int size)
    {
        string normalized = email.Trim().ToLowerInvariant();
        return TemplateFormatter.Create<object?>(template, variable => variable.Trim().ToLowerInvariant() switch
        {
            "name" => _ => HttpUtility.UrlEncode(name),
            "email" => _ => HttpUtility.UrlEncode(normalized),
            "md5" => _ => Hash(MD5.Create(), normalized),
            "sha1" => _ => Hash(SHA1.Create(), normalized),
            "sha256" => _ => Hash(SHA256.Create(), normalized),
            "imagesize" => _ => size.ToString(),
            _ => _ => null,
        })(null);
    }

    private static string? GravatarFallback(AvatarFallbackType? fallback) => fallback switch
    {
        AvatarFallbackType.Identicon => "identicon",
        AvatarFallbackType.MonsterId => "monsterid",
        AvatarFallbackType.Wavatar => "wavatar",
        AvatarFallbackType.Retro => "retro",
        AvatarFallbackType.Robohash => "robohash",
        _ => null,
    };

    // Gravatar specifies no encoding; upstream hashes UTF-8 (GravatarProvider.ComputeHash).
    private static string Hash(HashAlgorithm algorithm, string email)
    {
        using (algorithm)
        {
            return HexString.FromByteArray(algorithm.ComputeHash(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant())));
        }
    }

    // Upstream's FileSystemAvatarCache around the providers that download.
    private async Task<byte[]?> LoadAsync(string email, int size, IReadOnlyList<Uri?> candidates)
    {
        string path = Path.Join(_cacheFolder, $"{email}.{size}px.png");
        if (ReadCache(path) is { } cached)
        {
            return cached;
        }

        foreach (Uri? candidate in candidates)
        {
            if (candidate is null)
            {
                return null;
            }

            byte[]? image = await _downloader.DownloadAsync(candidate);
            if (image is not null && !(candidate.Host == "avatars.githubusercontent.com" && size != GitHubIdenticonSize
                                       && PngWidth(image) == GitHubIdenticonSize))
            {
                WriteCache(path, image);
                return image;
            }
        }

        return null;
    }

    private byte[]? ReadCache(string path)
    {
        try
        {
            FileInfo file = new(path);
            if (!file.Exists)
            {
                return null;
            }

            AvatarOptions options = _options();
            int days = options.CacheDays < 1 ? DefaultCacheDays : options.CacheDays;
            if (options.Provider != AvatarProvider.None && file.LastWriteTime < DateTime.Now.AddDays(-days))
            {
                file.Delete();
                return null;
            }

            return File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private void WriteCache(string path, byte[] image)
    {
        try
        {
            Directory.CreateDirectory(_cacheFolder);
            File.WriteAllBytes(path, image);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Trace.WriteLine($"Failed to cache the avatar '{path}'. Error: {ex.Message}");
        }
    }

    // The width in a PNG file's IHDR chunk (bytes 16 to 19, big-endian); null for another format.
    private static int? PngWidth(byte[] image)
        => image.Length >= 24 && image[0] == 0x89 && image[1] == (byte)'P' && image[2] == (byte)'N' && image[3] == (byte)'G'
            ? (image[16] << 24) | (image[17] << 16) | (image[18] << 8) | image[19]
            : null;
}

/// <summary>
///  Upstream's <c>AvatarDownloader</c>: at most ten downloads at a time, one request per address for 30 seconds, the
///  system proxy with the user's credentials, and nothing when the computer has no network.
/// </summary>
public sealed class HttpAvatarDownloader : IAvatarDownloader
{
    private const int MaxConcurrentDownloads = 10;
    private static readonly TimeSpan _requestCacheTime = TimeSpan.FromSeconds(30);

    private static readonly HttpClient _client = new(new HttpClientHandler
    {
        UseProxy = true, DefaultProxyCredentials = CredentialCache.DefaultCredentials
    });

    private readonly SemaphoreSlim _downloads = new(MaxConcurrentDownloads);
    private readonly ConcurrentDictionary<Uri, (DateTime Time, Task<byte[]?> Download)> _requests = new();

    public async Task<byte[]?> DownloadAsync(Uri uri)
    {
        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            return null;
        }

        DateTime now = DateTime.UtcNow;
        foreach ((Uri key, (DateTime time, Task<byte[]?> _)) in _requests)
        {
            if (now - time > _requestCacheTime)
            {
                _requests.TryRemove(key, out _);
            }
        }

        return await _requests.GetOrAdd(uri, _ => (now, Task.Run(() => FetchAsync(uri)))).Download;
    }

    private async Task<byte[]?> FetchAsync(Uri uri)
    {
        await _downloads.WaitAsync();
        try
        {
            if (uri.IsFile)
            {
                return File.Exists(uri.LocalPath) ? await File.ReadAllBytesAsync(uri.LocalPath) : null;
            }

            using HttpResponseMessage response = await _client.GetAsync(uri);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync() : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException
                                       or UnauthorizedAccessException)
        {
            Trace.WriteLine(ex.Message);
            return null;
        }
        finally
        {
            _downloads.Release();
        }
    }
}
