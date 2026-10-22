using System.Collections.ObjectModel;

namespace BecomeARapMcp.Api.Data.Seeding;

public sealed class SeedTrack
{
    public required int TrackNumber { get; init; }
    public required string Title { get; init; }
    public required int DurationSeconds { get; init; }
    public required bool Explicit { get; init; }
    public required string Genre { get; init; }
    public Collection<string> FeaturedArtists { get; init; } = [];
}
