using System.Net;
using System.Text.Json;
using BecomeARapMcp.Web.Models;
using BecomeARapMcp.Web.Services;
using Microsoft.AspNetCore.WebUtilities;

namespace BecomeARapMcp.Web.Tests;

public sealed class CatalogClientTests
{
    [Fact]
    public async Task CollectionReadsEveryPage()
    {
        using var handler = new StubHandler(request =>
        {
            var page = int.Parse(QueryHelpers.ParseQuery(request.RequestUri!.Query)["page"].ToString());
            return TestCatalog.Json(new PagedResponse<AlbumSummary>(page, 1, 3, [TestCatalog.Albums[page - 1]]));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        var albums = await new CatalogClient(http).GetAlbumsAsync();
        Assert.Equal(3, albums.Count);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("page=3", handler.Requests[2].Query);
    }

    [Fact]
    public async Task QueryIsEncodedAndArtistFilterIsPreserved()
    {
        using var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/artists")
                return TestCatalog.Json(new PagedResponse<ArtistSummary>(1, 100, 0, []));
            return TestCatalog.Json(new PagedResponse<Track>(1, 100, 0, []));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        var client = new CatalogClient(http);
        await client.SearchArtistsAsync("Dre & Snoop + ?");
        await client.GetAppearancesAsync(42);
        Assert.Equal("Dre & Snoop + ?", QueryHelpers.ParseQuery(handler.Requests[0].Query)["search"]);
        Assert.Equal("42", QueryHelpers.ParseQuery(handler.Requests[1].Query)["artistId"]);
    }

    [Fact]
    public async Task MissingDetailIsNotTreatedAsServerFailure()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        Assert.Null(await new CatalogClient(http).GetAlbumAsync(999));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"page\":1,\"pageSize\":100,\"totalCount\":1,\"items\":[]}")]
    [InlineData("{\"page\":2,\"pageSize\":100,\"totalCount\":0,\"items\":[]}")]
    public async Task InvalidResponsesDoNotBecomeEmptyResults(string body)
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        var error = await Record.ExceptionAsync(() => new CatalogClient(http).GetAlbumsAsync());
        Assert.True(error is JsonException or InvalidDataException);
    }

    [Fact]
    public async Task SearchGroupsArtistsAndDeduplicatesCreditsWithoutRemovingOverlap()
    {
        using var handler = new StubHandler(TestCatalog.Respond);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        var search = new CatalogSearch(new CatalogClient(http));
        var eminem = await search.SearchAsync("  EMINEM  ");
        Assert.Equal(2, eminem.Albums.Count);
        Assert.Equal(1, Assert.Single(eminem.AlbumArtists).ArtistId);
        Assert.Equal(1, Assert.Single(eminem.TrackArtists).ArtistId);
        Assert.Equal(2, eminem.Tracks.Count);

        var dido = await search.SearchAsync("dido");
        Assert.Empty(dido.Albums);
        Assert.Empty(dido.AlbumArtists);
        Assert.Equal(2, Assert.Single(dido.TrackArtists).ArtistId);
        Assert.Equal(2, dido.Tracks.Count);

        var stan = await search.SearchAsync("Stan");
        Assert.Empty(stan.TrackArtists);
        Assert.Equal("Stan", Assert.Single(stan.Tracks).Title);
        Assert.Equal(0, (await search.SearchAsync("not in the catalog")).Count);
    }

    [Fact]
    public async Task BlankSearchDoesNotCallTheApi()
    {
        using var handler = new StubHandler(_ => throw new InvalidOperationException("Unexpected request."));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        Assert.Equal(0, (await new CatalogSearch(new CatalogClient(http)).SearchAsync(" \t ")).Count);
        Assert.Empty(handler.Requests);
    }
}
