namespace BecomeARapMcp.Api.Data.Seeding;

public sealed class SeedArtist
{
    public required string Name { get; init; }
    public string? Biography { get; init; }
    public string? Country { get; init; }
    public int? FormedYear { get; init; }
}
