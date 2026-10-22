namespace BecomeARapMcp.Api.Data.Entities;

public sealed class SongArtist
{
    private Artist? _artist;
    private Song? _song;

    public int SongId { get; set; }
    public int ArtistId { get; set; }
    public required string Role { get; set; }

    public Song Song
    {
        get => _song ?? throw new InvalidOperationException("The credited song has not been loaded.");
        set => _song = value;
    }

    public Artist Artist
    {
        get => _artist ?? throw new InvalidOperationException("The credited artist has not been loaded.");
        set => _artist = value;
    }
}
