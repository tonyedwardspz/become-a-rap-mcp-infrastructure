namespace BecomeARapMcp.Api.Data.Entities;

public sealed class Album
{
    public int AlbumId { get; set; }
    public int ArtistId { get; set; }
    public required string Title { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public required string AlbumType { get; set; }

    public Artist Artist { get; set; } = null!;
    public ICollection<Song> Songs { get; } = new List<Song>();
}
