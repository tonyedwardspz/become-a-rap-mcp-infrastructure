using System.Collections.ObjectModel;

namespace BecomeARapMcp.Api.Data.Seeding;

public sealed class SeedAlbum
{
    public required string Artist { get; init; }
    public required string Title { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public required string AlbumType { get; init; }
    public required Uri SourceUrl { get; init; }
    public Uri? ExplicitSourceUrl { get; init; }
    public Uri? AlternateReleaseDateSourceUrl { get; init; }
    public required Collection<SeedTrack> Tracks { get; init; }
}
