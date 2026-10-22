namespace BecomeARapMcp.Api.Contracts;

public sealed record ArtistDto(int ArtistId, string Name, string? Biography, string? Country, int? FormedYear);

public sealed record ArtistDetailDto(
    int ArtistId,
    string Name,
    string? Biography,
    string? Country,
    int? FormedYear,
    IReadOnlyList<AlbumSummaryDto> Albums);
