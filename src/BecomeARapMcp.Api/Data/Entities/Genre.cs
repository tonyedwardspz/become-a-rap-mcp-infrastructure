namespace BecomeARapMcp.Api.Data.Entities;

public sealed class Genre
{
    public int GenreId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    public ICollection<Song> Songs { get; } = new List<Song>();
}
