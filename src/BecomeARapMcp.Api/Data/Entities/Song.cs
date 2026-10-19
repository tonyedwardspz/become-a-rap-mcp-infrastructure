namespace BecomeARapMcp.Api.Data.Entities;

public sealed class Song
{
    public int SongId { get; set; }
    public int AlbumId { get; set; }
    public required string Title { get; set; }
    public int DurationSeconds { get; set; }
    public int TrackNumber { get; set; }
    public bool Explicit { get; set; }
    public int GenreId { get; set; }

    public Album Album { get; set; } = null!;
    public Genre Genre { get; set; } = null!;
    public ICollection<SongArtist> SongArtists { get; } = new List<SongArtist>();
}
