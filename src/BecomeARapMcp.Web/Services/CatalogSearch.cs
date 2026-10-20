using BecomeARapMcp.Web.Models;

namespace BecomeARapMcp.Web.Services;

public sealed class CatalogSearch(CatalogClient catalog)
{
    public async Task<SearchResults> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        if (query.Length == 0)
        {
            return new SearchResults([], [], [], []);
        }

        var albumsTask = catalog.GetAlbumsAsync(cancellationToken);
        var artistsTask = catalog.SearchArtistsAsync(query, cancellationToken);
        var tracksTask = catalog.SearchTracksAsync(query, cancellationToken);
        await Task.WhenAll(albumsTask, artistsTask, tracksTask);

        var albums = await albumsTask;
        var artists = await artistsTask;
        var tracks = await tracksTask;
        var albumArtistIds = albums.Select(album => album.ArtistId).ToHashSet();
        bool Matches(string value) => value.Contains(query, StringComparison.OrdinalIgnoreCase);

        return new SearchResults(
            albums.Where(album => Matches(album.Title) || Matches(album.ArtistName))
                .OrderBy(album => album.ArtistName).ThenBy(album => album.Title).ToList(),
            artists.Where(artist => Matches(artist.Name) && albumArtistIds.Contains(artist.ArtistId))
                .DistinctBy(artist => artist.ArtistId).OrderBy(artist => artist.Name).ToList(),
            tracks.SelectMany(track => track.Artists).Where(artist => Matches(artist.Name))
                .DistinctBy(artist => artist.ArtistId).OrderBy(artist => artist.Name).ToList(),
            tracks.Where(track => Matches(track.Title) || Matches(track.AlbumTitle)
                    || track.Artists.Any(artist => Matches(artist.Name)))
                .DistinctBy(track => track.SongId).OrderBy(track => track.AlbumTitle)
                .ThenBy(track => track.TrackNumber).ToList());
    }
}
