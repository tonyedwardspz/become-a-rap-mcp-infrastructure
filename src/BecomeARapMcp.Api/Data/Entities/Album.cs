namespace BecomeARapMcp.Api.Data.Entities;

public sealed class Album
{
    private Artist? _artist;

    public int AlbumId { get; set; }
    public int ArtistId { get; set; }
    public required string Title { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public required string AlbumType { get; set; }

    public Artist Artist
    {
        get => _artist ?? throw new InvalidOperationException("The album's artist has not been loaded.");
        set => _artist = value;
    }

    public ICollection<Song> Songs { get; } = new List<Song>();
}
