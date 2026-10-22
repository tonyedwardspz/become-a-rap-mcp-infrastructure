using System.Text.Json;
using BecomeARapMcp.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BecomeARapMcp.Api.Data.Seeding;

public sealed class MusicCatalogSeeder(IWebHostEnvironment environment, ILogger<MusicCatalogSeeder> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    public async Task SeedAsync(MusicDbContext db, CancellationToken cancellationToken)
    {
        var seedPath = Path.Combine(environment.ContentRootPath, "Data", "Seed", "music-catalogue.json");
        var seed = await ReadSeedAsync(seedPath, cancellationToken);
        CatalogSeedValidator.Validate(seed);
        await MusicCatalogWriter.SeedAsync(db, seed, cancellationToken);
        logger.LogInformation(
            "Catalogue seed completed from {SeedPath}: {AlbumCount} albums and {TrackCount} tracks are present.",
            seedPath,
            seed.Albums.Count,
            seed.Albums.Sum(album => album.Tracks.Count));
    }

    private static async Task<CatalogSeedData> ReadSeedAsync(string seedPath, CancellationToken cancellationToken)
    {
        await using var seedStream = File.OpenRead(seedPath);
        return await JsonSerializer.DeserializeAsync<CatalogSeedData>(seedStream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException($"The catalogue seed file '{seedPath}' contained no data.");
    }
}

internal static class MusicCatalogWriter
{
    internal static async Task SeedAsync(
        MusicDbContext db,
        CatalogSeedData seed,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var artistsByName = await AddMissingArtistsAsync(db, seed, cancellationToken);
        var genresByName = await AddMissingGenresAsync(db, seed.Genres, cancellationToken);
        var albumsByKey = await AddMissingAlbumsAsync(db, seed.Albums, artistsByName, cancellationToken);
        var songsToCredit = await AddMissingSongsAsync(
            db,
            seed.Albums,
            artistsByName,
            genresByName,
            albumsByKey,
            cancellationToken);

        await AddMissingCreditsAsync(db, songsToCredit, artistsByName, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, Artist>> AddMissingArtistsAsync(
        MusicDbContext db,
        CatalogSeedData seed,
        CancellationToken cancellationToken)
    {
        var artists = await db.Artists.ToListAsync(cancellationToken);
        var artistsByName = artists
            .GroupBy(artist => artist.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var featuredArtists = seed.Albums
            .SelectMany(album => album.Tracks)
            .SelectMany(track => track.FeaturedArtists)
            .Select(name => new SeedArtist { Name = name });

        foreach (var artistData in seed.Artists.Concat(featuredArtists))
        {
            AddArtistIfMissing(db, artistData, artistsByName);
        }

        await db.SaveChangesAsync(cancellationToken);
        return artistsByName;
    }

    private static void AddArtistIfMissing(
        MusicDbContext db,
        SeedArtist artistData,
        IDictionary<string, Artist> artistsByName)
    {
        if (artistsByName.ContainsKey(artistData.Name))
        {
            return;
        }

        var artist = new Artist
        {
            Name = artistData.Name,
            Biography = artistData.Biography,
            Country = artistData.Country,
            FormedYear = artistData.FormedYear
        };
        db.Artists.Add(artist);
        artistsByName.Add(artist.Name, artist);
    }

    private static async Task<Dictionary<string, Genre>> AddMissingGenresAsync(
        MusicDbContext db,
        IEnumerable<SeedGenre> seedGenres,
        CancellationToken cancellationToken)
    {
        var genres = await db.Genres.ToListAsync(cancellationToken);
        var genresByName = genres
            .GroupBy(genre => genre.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var genreData in seedGenres)
        {
            AddGenreIfMissing(db, genreData, genresByName);
        }

        await db.SaveChangesAsync(cancellationToken);
        return genresByName;
    }

    private static void AddGenreIfMissing(
        MusicDbContext db,
        SeedGenre genreData,
        IDictionary<string, Genre> genresByName)
    {
        if (genresByName.ContainsKey(genreData.Name))
        {
            return;
        }

        var genre = new Genre
        {
            Name = genreData.Name,
            Description = genreData.Description
        };
        db.Genres.Add(genre);
        genresByName.Add(genre.Name, genre);
    }

    private static async Task<Dictionary<string, Album>> AddMissingAlbumsAsync(
        MusicDbContext db,
        IEnumerable<SeedAlbum> seedAlbums,
        IReadOnlyDictionary<string, Artist> artistsByName,
        CancellationToken cancellationToken)
    {
        var albums = await db.Albums.ToListAsync(cancellationToken);
        var albumsByKey = albums
            .GroupBy(album => AlbumKey(album.ArtistId, album.Title), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var albumData in seedAlbums)
        {
            AddAlbumIfMissing(db, albumData, artistsByName, albumsByKey);
        }

        await db.SaveChangesAsync(cancellationToken);
        return albumsByKey;
    }

    private static void AddAlbumIfMissing(
        MusicDbContext db,
        SeedAlbum albumData,
        IReadOnlyDictionary<string, Artist> artistsByName,
        IDictionary<string, Album> albumsByKey)
    {
        var artist = artistsByName[albumData.Artist];
        var albumKey = AlbumKey(artist.ArtistId, albumData.Title);
        if (albumsByKey.ContainsKey(albumKey))
        {
            return;
        }

        var album = new Album
        {
            ArtistId = artist.ArtistId,
            Artist = artist,
            Title = albumData.Title,
            ReleaseDate = albumData.ReleaseDate,
            AlbumType = albumData.AlbumType
        };
        db.Albums.Add(album);
        albumsByKey.Add(albumKey, album);
    }

    private static async Task<List<(Song Song, SeedAlbum Album, SeedTrack Track)>> AddMissingSongsAsync(
        MusicDbContext db,
        IEnumerable<SeedAlbum> seedAlbums,
        IReadOnlyDictionary<string, Artist> artistsByName,
        IReadOnlyDictionary<string, Genre> genresByName,
        IReadOnlyDictionary<string, Album> albumsByKey,
        CancellationToken cancellationToken)
    {
        var songs = await db.Songs.ToListAsync(cancellationToken);
        var songsByKey = songs.ToDictionary(song => (song.AlbumId, song.TrackNumber));
        var songsToCredit = new List<(Song Song, SeedAlbum Album, SeedTrack Track)>();

        foreach (var albumData in seedAlbums)
        {
            var artist = artistsByName[albumData.Artist];
            var album = albumsByKey[AlbumKey(artist.ArtistId, albumData.Title)];
            foreach (var trackData in albumData.Tracks)
            {
                AddSongIfMissing(db, albumData, trackData, album, genresByName, songsByKey, songsToCredit);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return songsToCredit;
    }

    private static void AddSongIfMissing(
        MusicDbContext db,
        SeedAlbum albumData,
        SeedTrack trackData,
        Album album,
        IReadOnlyDictionary<string, Genre> genresByName,
        IDictionary<(int AlbumId, int TrackNumber), Song> songsByKey,
        ICollection<(Song Song, SeedAlbum Album, SeedTrack Track)> songsToCredit)
    {
        if (songsByKey.TryGetValue((album.AlbumId, trackData.TrackNumber), out var existingSong))
        {
            EnsureTrackTitleMatches(existingSong, trackData, album);
            songsToCredit.Add((existingSong, albumData, trackData));
            return;
        }

        var genre = genresByName[trackData.Genre];
        var song = new Song
        {
            AlbumId = album.AlbumId,
            Album = album,
            Title = trackData.Title,
            DurationSeconds = trackData.DurationSeconds,
            TrackNumber = trackData.TrackNumber,
            Explicit = trackData.Explicit,
            GenreId = genre.GenreId,
            Genre = genre
        };
        db.Songs.Add(song);
        songsByKey.Add((song.AlbumId, song.TrackNumber), song);
        songsToCredit.Add((song, albumData, trackData));
    }

    private static void EnsureTrackTitleMatches(Song song, SeedTrack track, Album album)
    {
        if (!string.Equals(song.Title, track.Title, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Track {track.TrackNumber} on '{album.Title}' is already stored as '{song.Title}', not '{track.Title}'.");
        }
    }

    private static async Task AddMissingCreditsAsync(
        MusicDbContext db,
        IEnumerable<(Song Song, SeedAlbum Album, SeedTrack Track)> songsToCredit,
        IReadOnlyDictionary<string, Artist> artistsByName,
        CancellationToken cancellationToken)
    {
        var credits = songsToCredit.ToList();
        var songIds = credits.Select(item => item.Song.SongId).ToHashSet();
        var songArtistLinks = await db.SongArtists
            .Where(link => songIds.Contains(link.SongId))
            .ToListAsync(cancellationToken);
        var existingLinks = songArtistLinks.ToDictionary(link => (link.SongId, link.ArtistId));

        foreach (var (song, albumData, trackData) in credits)
        {
            AddCreditsForTrack(db, song, albumData, trackData, artistsByName, existingLinks);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void AddCreditsForTrack(
        MusicDbContext db,
        Song song,
        SeedAlbum albumData,
        SeedTrack trackData,
        IReadOnlyDictionary<string, Artist> artistsByName,
        IDictionary<(int SongId, int ArtistId), SongArtist> existingLinks)
    {
        var primaryArtist = artistsByName[albumData.Artist];
        AddSongArtistIfMissing(song, primaryArtist, SongArtistRoles.Primary, existingLinks, db);

        foreach (var featuredArtistName in trackData.FeaturedArtists)
        {
            var featuredArtist = artistsByName[featuredArtistName];
            AddSongArtistIfMissing(song, featuredArtist, SongArtistRoles.Featured, existingLinks, db);
        }
    }

    private static void AddSongArtistIfMissing(
        Song song,
        Artist artist,
        string role,
        IDictionary<(int SongId, int ArtistId), SongArtist> existingLinks,
        MusicDbContext db)
    {
        var key = (song.SongId, artist.ArtistId);
        if (existingLinks.TryGetValue(key, out var existingLink))
        {
            PromoteToPrimaryArtist(role, existingLink);
            return;
        }

        var link = new SongArtist
        {
            SongId = song.SongId,
            Song = song,
            ArtistId = artist.ArtistId,
            Artist = artist,
            Role = role
        };
        db.SongArtists.Add(link);
        existingLinks.Add(key, link);
    }

    private static void PromoteToPrimaryArtist(string role, SongArtist existingLink)
    {
        if (role == SongArtistRoles.Primary && existingLink.Role != SongArtistRoles.Primary)
        {
            existingLink.Role = SongArtistRoles.Primary;
        }
    }

    private static string AlbumKey(int artistId, string title) => $"{artistId}\u001f{title}";
}
