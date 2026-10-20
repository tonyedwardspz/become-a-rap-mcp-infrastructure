using System.Net;
using BecomeARapMcp.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace BecomeARapMcp.Web.Tests;

public sealed class ArtworkTests
{
    [Fact]
    public async Task MatchingArtworkAndMissesAreCachedAndIdentitiesAreChecked()
    {
        using var handler = new StubHandler(request => request.RequestUri!.AbsolutePath.EndsWith("search.php")
            ? TestCatalog.Json(new { artists = new[] {
                new { strArtist = "Wrong artist", strArtistThumb = "https://images.example/wrong.jpg" },
                new { strArtist = "Eminem", strArtistThumb = "https://images.example/artist.jpg" } } })
            : TestCatalog.Json(new { album = new[] {
                new { strArtist = "Wrong artist", strAlbum = "Wrong album", strAlbumThumb = "https://images.example/wrong.jpg" },
                new { strArtist = "Eminem", strAlbum = "The Marshall Mathers LP", strAlbumThumb = "https://images.example/album.jpg" } } }));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = Create(handler, cache);
        Assert.Equal("https://images.example/artist.jpg", await service.FindAsync("Eminem", null));
        Assert.Equal("https://images.example/artist.jpg", await service.FindAsync("eminem", null));
        Assert.Equal("https://images.example/album.jpg", await service.FindAsync("Eminem", "The Marshall Mathers LP"));
        Assert.Null(await service.FindAsync("Unknown artist", null));
        Assert.Null(await service.FindAsync("Unknown artist", null));
        Assert.Equal(3, handler.Requests.Count);
    }

    [Theory]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task ProviderFailuresUsePlaceholderAndAreBrieflyCached(int status)
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage((HttpStatusCode)status));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = Create(handler, cache);
        Assert.Null(await service.FindAsync("Eminem", null));
        Assert.Null(await service.FindAsync("Eminem", null));
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TimeoutAndNetworkFailureUsePlaceholder(bool timeout)
    {
        using var handler = new StubHandler(_ => throw (timeout
            ? new TaskCanceledException("Provider timeout.")
            : new HttpRequestException("No network.")));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        Assert.Null(await Create(handler, cache).FindAsync("Eminem", null));
    }

    [Fact]
    public async Task NonHttpsImagesAreRejected()
    {
        using var handler = new StubHandler(_ => TestCatalog.Json(new { artists = new[] {
            new { strArtist = "Eminem", strArtistThumb = "http://images.example/artist.jpg" } } }));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        Assert.Null(await Create(handler, cache).FindAsync("Eminem", null));
    }

    [Fact]
    public async Task RequestsRespectThirtyPerMinuteAndResumeAfterWindow()
    {
        using var handler = new StubHandler(_ => TestCatalog.Json(new { artists = (object?)null }));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var clock = new TestClock();
        var service = Create(handler, cache, clock);
        for (var index = 0; index < 35; index++)
            Assert.Null(await service.FindAsync($"Artist {index}", null));
        Assert.Equal(30, handler.Requests.Count);
        clock.Now += TimeSpan.FromMinutes(1);
        Assert.Null(await service.FindAsync("Next artist", null));
        Assert.Equal(31, handler.Requests.Count);
    }

    private static ArtworkService Create(StubHandler handler, IMemoryCache cache, TimeProvider? clock = null) =>
        new(new StubClientFactory(handler), cache, clock ?? TimeProvider.System, NullLogger<ArtworkService>.Instance);

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

internal sealed class StubClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://www.theaudiodb.com/api/v1/json/123/") };
}
