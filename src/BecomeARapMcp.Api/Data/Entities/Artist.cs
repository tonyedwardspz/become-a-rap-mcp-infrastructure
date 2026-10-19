namespace BecomeARapMcp.Api.Data.Entities;

public sealed class Artist
{
    public int ArtistId { get; set; }
    public required string Name { get; set; }
    public string? Biography { get; set; }
    public string? Country { get; set; }
    public int? FormedYear { get; set; }

    public ICollection<Album> Albums { get; } = new List<Album>();
    public ICollection<SongArtist> SongArtists { get; } = new List<SongArtist>();
}
