namespace BecomeARapMcp.Api.Data.Seeding;

internal sealed class CatalogSeedData
{
    public required List<SeedArtist> Artists { get; init; }
    public required List<SeedGenre> Genres { get; init; }
    public required List<SeedAlbum> Albums { get; init; }
}

internal sealed class SeedArtist
{
    public required string Name { get; init; }
    public string? Biography { get; init; }
    public string? Country { get; init; }
    public int? FormedYear { get; init; }
}

internal sealed class SeedGenre
{
    public required string Name { get; init; }
    public string? Description { get; init; }
}

internal sealed class SeedAlbum
{
    public required string Artist { get; init; }
    public required string Title { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public required string AlbumType { get; init; }
    public required string SourceUrl { get; init; }
    public string? ExplicitSourceUrl { get; init; }
    public string? AlternateReleaseDateSourceUrl { get; init; }
    public required List<SeedTrack> Tracks { get; init; }
}

internal sealed class SeedTrack
{
    public required int TrackNumber { get; init; }
    public required string Title { get; init; }
    public required int DurationSeconds { get; init; }
    public required bool Explicit { get; init; }
    public required string Genre { get; init; }
    public List<string> FeaturedArtists { get; init; } = [];
}
