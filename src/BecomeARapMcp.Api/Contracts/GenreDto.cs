namespace BecomeARapMcp.Api.Contracts;

public sealed record GenreDto(int GenreId, string Name, string? Description, int SongCount);
