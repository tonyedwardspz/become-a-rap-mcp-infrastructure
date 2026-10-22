using System.Collections.ObjectModel;

namespace BecomeARapMcp.Api.Data.Seeding;

public sealed class CatalogSeedData
{
    public required Collection<SeedArtist> Artists { get; init; }
    public required Collection<SeedGenre> Genres { get; init; }
    public required Collection<SeedAlbum> Albums { get; init; }
}
