namespace BecomeARapMcp.Api.Contracts;

public sealed record SongArtistDto(int ArtistId, string Name, string Role);

public sealed record SongDto(
    int SongId,
    string Title,
    int DurationSeconds,
    int TrackNumber,
    bool Explicit,
    int AlbumId,
    string AlbumTitle,
    DateOnly? AlbumReleaseDate,
    int GenreId,
    string GenreName,
    IReadOnlyList<SongArtistDto> Artists);
