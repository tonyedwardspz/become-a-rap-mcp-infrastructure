namespace BecomeARapMcp.Api.Contracts;

public sealed record AlbumSummaryDto(
    int AlbumId,
    string Title,
    DateOnly? ReleaseDate,
    string AlbumType,
    int ArtistId,
    string ArtistName);

public sealed record AlbumDetailDto(
    int AlbumId,
    string Title,
    DateOnly? ReleaseDate,
    string AlbumType,
    int ArtistId,
    string ArtistName,
    IReadOnlyList<SongDto> Songs);
