namespace BecomeARapMcp.Api.Data.Entities;

public sealed class Song
{
    private Album? _album;
    private Genre? _genre;

    public int SongId { get; set; }
    public int AlbumId { get; set; }
    public required string Title { get; set; }
    public int DurationSeconds { get; set; }
    public int TrackNumber { get; set; }
    public bool Explicit { get; set; }
    public int GenreId { get; set; }

    public Album Album
    {
        get => _album ?? throw new InvalidOperationException("The song's album has not been loaded.");
        set => _album = value;
    }

    public Genre Genre
    {
        get => _genre ?? throw new InvalidOperationException("The song's genre has not been loaded.");
        set => _genre = value;
    }

    public ICollection<SongArtist> SongArtists { get; } = new List<SongArtist>();
}
