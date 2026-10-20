using System.Net;
using BecomeARapMcp.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BecomeARapMcp.Web.Tests;

public sealed class PageTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/albums/1")]
    [InlineData("/artists/1")]
    [InlineData("/artists/2")]
    [InlineData("/tracks/1")]
    [InlineData("/search?q=Dido")]
    [InlineData("/search")]
    [InlineData("/search?q=not-found")]
    [InlineData("/not-found", 404)]
    [InlineData("/albums/999", 404)]
    [InlineData("/artists/999", 404)]
    [InlineData("/tracks/999", 404)]
    [InlineData("/unknown-page", 404)]
    [InlineData("/Error")]
    public async Task EveryPageHasWorkingTopLevelSearch(string path, int status = 200)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Contains("action=\"/search\" method=\"get\"", html);
        Assert.Contains("name=\"q\"", html);
        Assert.True(html.IndexOf("id=\"catalog-search\"", StringComparison.Ordinal)
            < html.IndexOf("<main", StringComparison.Ordinal));
        Assert.DoesNotContain("<!--Blazor:{\"type\":\"server\"", html);
    }

    [Fact]
    public async Task HomeGroupsAlbumsAndAlbumAndArtistPagesExposeCredits()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var home = await client.GetStringAsync("/");
        Assert.Equal(1, home.Split("href=\"/artists/1\"").Length - 1);
        Assert.Contains("href=\"/albums/1\"", home);
        Assert.True(home.IndexOf("Dr. Dre", StringComparison.Ordinal) < home.IndexOf("Eminem", StringComparison.Ordinal));
        var album = await client.GetStringAsync("/albums/1");
        Assert.Contains("href=\"/tracks/1\"", album);
        Assert.Contains("href=\"/artists/2\"", album);
        Assert.Contains("(featured)", album);
        Assert.Contains("6:44", album);
        Assert.Contains("Album cover unavailable", album);
        var artist = await client.GetStringAsync("/artists/2");
        Assert.Contains("No albums in this section.", artist);
        Assert.Contains("Track appearances", artist);
        Assert.Contains("Stan", artist);
        Assert.Contains("Artist photo unavailable", artist);
    }

    [Fact]
    public async Task SearchRendersGroupsAndPreservesEscapedQuery()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/search?q=Dido");
        Assert.Contains("value=\"Dido\"", html);
        Assert.Contains(">Album artists ", html);
        Assert.Contains(">Track artists ", html);
        Assert.Contains(">Tracks ", html);
        Assert.Contains("href=\"/artists/2\"", html);
        Assert.Contains("Stan", html);
        var escaped = await client.GetStringAsync("/search?q=%3Cscript%3E");
        Assert.DoesNotContain("<strong><script>", escaped);
        Assert.Contains("&lt;script&gt;", escaped);
        Assert.Contains("No results found.", escaped);
    }

    [Fact]
    public async Task DetailPagesLookUpArtworkUsingActualArtistAndAlbumNames()
    {
        using var factory = CreateFactory(returnArtwork: true);
        using var client = factory.CreateClient();
        var album = await client.GetStringAsync("/albums/1");
        Assert.Contains("src=\"https://images.example/album.jpg\"", album);
        Assert.Contains("Artwork via", album);
        var artist = await client.GetStringAsync("/artists/1");
        Assert.Contains("src=\"https://images.example/artist.jpg\"", artist);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/albums/1")]
    [InlineData("/artists/2")]
    [InlineData("/tracks/1")]
    [InlineData("/search?q=Dido")]
    public async Task ApiFailureShowsAnExplicitRetryInsteadOfEmptyResults(string path)
    {
        using var factory = CreateFactory(failApi: true);
        using var client = factory.CreateClient();
        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Catalog unavailable", html);
        Assert.Contains("The catalog is unavailable right now. Please retry.", html);
        Assert.Contains("Retry this page", html);
        Assert.DoesNotContain("No results found.", html);
    }

    private static WebApplicationFactory<Program> CreateFactory(bool failApi = false, bool returnArtwork = false) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<CatalogClient>();
                services.AddTransient(_ => new CatalogClient(new HttpClient(new StubHandler(request =>
                    failApi ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : TestCatalog.Respond(request)))
                    { BaseAddress = new Uri("http://api") }));
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(new StubClientFactory(new StubHandler(request =>
                {
                    var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.RequestUri!.Query);
                    var matchArtist = returnArtwork && query["s"] == "Eminem";
                    var matchAlbum = matchArtist && query.TryGetValue("a", out var title)
                        && title == "The Marshall Mathers LP";
                    return TestCatalog.Json(new
                    {
                        artists = matchArtist
                            ? new[] { new { strArtist = "Eminem", strArtistThumb = "https://images.example/artist.jpg" } }
                            : null,
                        album = matchAlbum
                            ? new[] { new { strArtist = "Eminem", strAlbum = "The Marshall Mathers LP", strAlbumThumb = "https://images.example/album.jpg" } }
                            : null
                    });
                })));
            });
        });
}
