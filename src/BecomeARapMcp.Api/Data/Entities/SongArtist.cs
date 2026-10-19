namespace BecomeARapMcp.Api.Data.Entities;

public static class SongArtistRoles
{
    public const string Primary = "Primary";
    public const string Featured = "Featured";
}

public sealed class SongArtist
{
    public int SongId { get; set; }
    public int ArtistId { get; set; }
    public required string Role { get; set; }

    public Song Song { get; set; } = null!;
    public Artist Artist { get; set; } = null!;
}
