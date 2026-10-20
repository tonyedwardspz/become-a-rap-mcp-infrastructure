using System.Net;
using System.Net.Http.Json;
using BecomeARapMcp.Web.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace BecomeARapMcp.Web.Tests;

internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(respond(request));
    }
}

internal static class TestCatalog
{
    public static readonly AlbumSummary[] Albums =
    [
        new(1, "The Marshall Mathers LP", new DateOnly(2000, 5, 23), "Album", 1, "Eminem"),
        new(2, "The Slim Shady LP", new DateOnly(1999, 2, 23), "Album", 1, "Eminem"),
        new(3, "2001", null, "Album", 3, "Dr. Dre")
    ];
    public static readonly ArtistSummary[] Artists =
    [
        new(1, "Eminem", "A catalog biography.", "US", 1988),
        new(2, "Dido", null, "GB", null),
        new(3, "Dr. Dre", null, "US", null)
    ];
    public static readonly Track[] Tracks =
    [
        new(1, "Stan", 404, 3, true, 1, Albums[0].Title, Albums[0].ReleaseDate, 1, "Hip Hop",
            [new(1, "Eminem", "Primary"), new(2, "Dido", "Featured")]),
        new(2, "Another credit", 125, 4, false, 1, Albums[0].Title, Albums[0].ReleaseDate, 1, "Hip Hop",
            [new(1, "Eminem", "Primary"), new(2, "Dido", "Featured")])
    ];

    public static HttpResponseMessage Respond(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        var query = QueryHelpers.ParseQuery(request.RequestUri.Query);
        var search = query.TryGetValue("search", out var term) ? term.ToString() : "";
        bool Matches(string value) => value.Contains(search, StringComparison.OrdinalIgnoreCase);
        return path switch
        {
            "/albums" => Json(new PagedResponse<AlbumSummary>(1, 100, Albums.Length, Albums)),
            "/artists" => Json(new PagedResponse<ArtistSummary>(1, 100,
                Artists.Count(artist => Matches(artist.Name)), Artists.Where(artist => Matches(artist.Name)).ToList())),
            "/songs" => TrackPage(),
            "/albums/1" => Json(new AlbumDetail(1, Albums[0].Title, Albums[0].ReleaseDate, "Album", 1, "Eminem", Tracks)),
            "/artists/1" => Json(new ArtistDetail(1, "Eminem", "A catalog biography.", "US", 1988,
                Albums.Where(album => album.ArtistId == 1).ToList())),
            "/artists/2" => Json(new ArtistDetail(2, "Dido", null, "GB", null, [])),
            "/songs/1" => Json(Tracks[0]),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        };

        HttpResponseMessage TrackPage()
        {
            var items = Tracks.Where(track => Matches(track.Title) || Matches(track.AlbumTitle)
                || track.Artists.Any(artist => Matches(artist.Name)));
            if (query.TryGetValue("artistId", out var artistId))
            {
                items = items.Where(track => track.Artists.Any(artist => artist.ArtistId.ToString() == artistId));
            }
            var list = items.ToList();
            return Json(new PagedResponse<Track>(1, 100, list.Count, list));
        }
    }

    public static HttpResponseMessage Json<T>(T value) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}
