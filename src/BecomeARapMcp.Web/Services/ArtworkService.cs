using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace BecomeARapMcp.Web.Services;

public sealed class ArtworkService(
    IHttpClientFactory clients, IMemoryCache cache, TimeProvider clock, ILogger<ArtworkService> logger)
{
    private readonly Queue<DateTimeOffset> _requests = new();

    public async Task<string?> FindAsync(string artist, string? album, CancellationToken cancellationToken = default)
    {
        var key = (artist.ToUpperInvariant(), album?.ToUpperInvariant());
        if (cache.TryGetValue(key, out string? cached))
        {
            return cached;
        }

        lock (_requests)
        {
            var now = clock.GetUtcNow();
            while (_requests.TryPeek(out var oldest) && now - oldest >= TimeSpan.FromMinutes(1))
            {
                _requests.Dequeue();
            }

            if (_requests.Count >= 30)
            {
                logger.LogWarning("Artwork lookup skipped because the free provider request budget is exhausted.");
                return null;
            }

            _requests.Enqueue(now);
        }

        var path = album is null
            ? $"search.php?s={Uri.EscapeDataString(artist)}"
            : $"searchalbum.php?s={Uri.EscapeDataString(artist)}&a={Uri.EscapeDataString(album)}";
        try
        {
            using var client = clients.CreateClient("artwork");
            var response = await client.GetFromJsonAsync<ArtworkResponse>(path, cancellationToken)
                ?? throw new JsonException("The artwork provider returned an empty response.");
            var match = album is null
                ? response.Artists?.FirstOrDefault(item => SameName(item.Artist, artist))?.ArtistThumb
                : response.Albums?.FirstOrDefault(item => SameName(item.Artist, artist)
                    && SameName(item.Album, album))?.AlbumThumb;
            var url = Uri.TryCreate(match, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
                ? uri.AbsoluteUri : null;
            cache.Set(key, url, url is null ? TimeSpan.FromMinutes(5) : TimeSpan.FromHours(1));
            return url;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Artwork lookup unavailable for {Artist}, {Album}.", artist, album);
            cache.Set<string?>(key, null, TimeSpan.FromMinutes(1));
            return null;
        }
    }

    private static bool SameName(string? actual, string expected) =>
        string.Equals(actual?.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);

    private sealed record ArtworkResponse(
        [property: JsonPropertyName("artists")] IReadOnlyList<ArtworkItem>? Artists,
        [property: JsonPropertyName("album")] IReadOnlyList<ArtworkItem>? Albums);

    private sealed record ArtworkItem(
        [property: JsonPropertyName("strArtist")] string? Artist,
        [property: JsonPropertyName("strAlbum")] string? Album,
        [property: JsonPropertyName("strArtistThumb")] string? ArtistThumb,
        [property: JsonPropertyName("strAlbumThumb")] string? AlbumThumb);
}
