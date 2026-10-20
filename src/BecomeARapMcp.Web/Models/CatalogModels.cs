namespace BecomeARapMcp.Web.Models;

public sealed record PagedResponse<T>(int Page, int PageSize, int TotalCount, IReadOnlyList<T> Items);

public sealed record AlbumSummary(
    int AlbumId, string Title, DateOnly? ReleaseDate, string AlbumType, int ArtistId, string ArtistName);

public sealed record AlbumDetail(
    int AlbumId, string Title, DateOnly? ReleaseDate, string AlbumType, int ArtistId, string ArtistName,
    IReadOnlyList<Track> Songs);

public sealed record ArtistSummary(int ArtistId, string Name, string? Biography, string? Country, int? FormedYear);

public sealed record ArtistDetail(
    int ArtistId, string Name, string? Biography, string? Country, int? FormedYear,
    IReadOnlyList<AlbumSummary> Albums);

public sealed record ArtistCredit(int ArtistId, string Name, string Role);

public sealed record Track(
    int SongId, string Title, int DurationSeconds, int TrackNumber, bool Explicit, int AlbumId,
    string AlbumTitle, DateOnly? AlbumReleaseDate, int GenreId, string GenreName,
    IReadOnlyList<ArtistCredit> Artists);

public sealed record SearchResults(
    IReadOnlyList<AlbumSummary> Albums, IReadOnlyList<ArtistSummary> AlbumArtists,
    IReadOnlyList<ArtistCredit> TrackArtists, IReadOnlyList<Track> Tracks)
{
    public int Count => Albums.Count + AlbumArtists.Count + TrackArtists.Count + Tracks.Count;
}
