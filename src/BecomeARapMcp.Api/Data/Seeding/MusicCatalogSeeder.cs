using System.Text.Json;
using BecomeARapMcp.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BecomeARapMcp.Api.Data.Seeding;

public sealed class MusicCatalogSeeder(IWebHostEnvironment environment, ILogger<MusicCatalogSeeder> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task SeedAsync(MusicDbContext db, CancellationToken cancellationToken)
    {
        var seedPath = Path.Combine(environment.ContentRootPath, "Data", "Seed", "music-catalogue.json");
        await using var seedStream = File.OpenRead(seedPath);
        var seed = await JsonSerializer.DeserializeAsync<CatalogSeedData>(seedStream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException($"The catalogue seed file '{seedPath}' contained no data.");

        ValidateSeed(seed);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var artists = await db.Artists.ToListAsync(cancellationToken);
        var artistsByName = artists
            .GroupBy(artist => artist.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var artistData in seed.Artists.Concat(
                     seed.Albums.SelectMany(album => album.Tracks.SelectMany(track => track.FeaturedArtists))
                         .Select(name => new SeedArtist { Name = name })
                         .ToList()))
        {
            if (artistsByName.ContainsKey(artistData.Name))
            {
                continue;
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

        await db.SaveChangesAsync(cancellationToken);

        var genres = await db.Genres.ToListAsync(cancellationToken);
        var genresByName = genres
            .GroupBy(genre => genre.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var genreData in seed.Genres)
        {
            if (genresByName.ContainsKey(genreData.Name))
            {
                continue;
            }

            var genre = new Genre
            {
                Name = genreData.Name,
                Description = genreData.Description
            };
            db.Genres.Add(genre);
            genresByName.Add(genre.Name, genre);
        }

        await db.SaveChangesAsync(cancellationToken);

        var albums = await db.Albums.ToListAsync(cancellationToken);
        var albumsByKey = albums
            .GroupBy(album => AlbumKey(album.ArtistId, album.Title), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var albumData in seed.Albums)
        {
            var albumKey = AlbumKey(artistsByName[albumData.Artist].ArtistId, albumData.Title);
            if (albumsByKey.ContainsKey(albumKey))
            {
                continue;
            }

            var album = new Album
            {
                ArtistId = artistsByName[albumData.Artist].ArtistId,
                Title = albumData.Title,
                ReleaseDate = albumData.ReleaseDate,
                AlbumType = albumData.AlbumType
            };
            db.Albums.Add(album);
            albumsByKey.Add(albumKey, album);
        }

        await db.SaveChangesAsync(cancellationToken);

        var songs = await db.Songs.ToListAsync(cancellationToken);
        var songsByKey = songs.ToDictionary(song => (song.AlbumId, song.TrackNumber));
        var songsToCredit = new List<(Song Song, SeedAlbum Album, SeedTrack Track)>();

        foreach (var albumData in seed.Albums)
        {
            var album = albumsByKey[AlbumKey(artistsByName[albumData.Artist].ArtistId, albumData.Title)];
            foreach (var trackData in albumData.Tracks)
            {
                if (songsByKey.TryGetValue((album.AlbumId, trackData.TrackNumber), out var existingSong))
                {
                    if (!string.Equals(existingSong.Title, trackData.Title, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            $"Track {trackData.TrackNumber} on '{album.Title}' is already stored as '{existingSong.Title}', not '{trackData.Title}'.");
                    }

                    songsToCredit.Add((existingSong, albumData, trackData));
                    continue;
                }

                var song = new Song
                {
                    AlbumId = album.AlbumId,
                    Title = trackData.Title,
                    DurationSeconds = trackData.DurationSeconds,
                    TrackNumber = trackData.TrackNumber,
                    Explicit = trackData.Explicit,
                    GenreId = genresByName[trackData.Genre].GenreId
                };
                db.Songs.Add(song);
                songsByKey.Add((song.AlbumId, song.TrackNumber), song);
                songsToCredit.Add((song, albumData, trackData));
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var songIds = songsToCredit.Select(item => item.Song.SongId).ToHashSet();
        var songArtistLinks = await db.SongArtists
            .Where(link => songIds.Contains(link.SongId))
            .ToListAsync(cancellationToken);
        var existingLinks = songArtistLinks
            .ToDictionary(link => (link.SongId, link.ArtistId));

        foreach (var (song, albumData, trackData) in songsToCredit)
        {
            var primaryArtist = artistsByName[albumData.Artist];
            AddSongArtistIfMissing(song, primaryArtist, SongArtistRoles.Primary, existingLinks, db);

            foreach (var featuredArtistName in trackData.FeaturedArtists)
            {
                var featuredArtist = artistsByName[featuredArtistName];
                AddSongArtistIfMissing(song, featuredArtist, SongArtistRoles.Featured, existingLinks, db);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Catalogue seed completed from {SeedPath}: {AlbumCount} albums and {TrackCount} tracks are present.",
            seedPath,
            seed.Albums.Count,
            seed.Albums.Sum(album => album.Tracks.Count));
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
            if (role == SongArtistRoles.Primary && existingLink.Role != SongArtistRoles.Primary)
            {
                existingLink.Role = SongArtistRoles.Primary;
            }

            return;
        }

        var link = new SongArtist
        {
            SongId = song.SongId,
            ArtistId = artist.ArtistId,
            Role = role
        };
        db.SongArtists.Add(link);
        existingLinks.Add(key, link);
    }

    private static string AlbumKey(int artistId, string title) => $"{artistId}\u001f{title}";

    private static void ValidateSeed(CatalogSeedData seed)
    {
        if (seed.Artists.Count == 0 || seed.Genres.Count == 0 || seed.Albums.Count == 0)
        {
            throw new InvalidDataException("The catalogue seed must contain artists, genres, and albums.");
        }

        foreach (var album in seed.Albums)
        {
            if (!Uri.TryCreate(album.SourceUrl, UriKind.Absolute, out var sourceUri)
                || sourceUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidDataException($"Album '{album.Title}' has an invalid release source URL.");
            }

            if (album.ExplicitSourceUrl is not null
                && (!Uri.TryCreate(album.ExplicitSourceUrl, UriKind.Absolute, out var explicitSourceUri)
                    || explicitSourceUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidDataException($"Album '{album.Title}' has an invalid explicit-metadata source URL.");
            }

            if (album.AlternateReleaseDateSourceUrl is not null
                && (!Uri.TryCreate(album.AlternateReleaseDateSourceUrl, UriKind.Absolute, out var alternateDateSourceUri)
                    || alternateDateSourceUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidDataException($"Album '{album.Title}' has an invalid alternate release-date source URL.");
            }

            if (!string.Equals(album.AlbumType, "Album", StringComparison.Ordinal)
                && !string.Equals(album.AlbumType, "EP", StringComparison.Ordinal)
                && !string.Equals(album.AlbumType, "Single", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Album '{album.Title}' has unsupported type '{album.AlbumType}'.");
            }

            if (!seed.Artists.Any(artist => string.Equals(artist.Name, album.Artist, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException($"Album '{album.Title}' refers to unknown artist '{album.Artist}'.");
            }

            if (album.Tracks.Count == 0)
            {
                throw new InvalidDataException($"Album '{album.Title}' has no track listing.");
            }

            if (album.Tracks.Any(track => track.TrackNumber < 1 || track.DurationSeconds < 0 || string.IsNullOrWhiteSpace(track.Title)))
            {
                throw new InvalidDataException($"Album '{album.Title}' has an invalid track entry.");
            }

            if (album.Tracks.Any(track =>
                    !seed.Genres.Any(genre => string.Equals(genre.Name, track.Genre, StringComparison.OrdinalIgnoreCase))
                    || track.FeaturedArtists.Any(string.IsNullOrWhiteSpace)))
            {
                throw new InvalidDataException($"Album '{album.Title}' has a track with an unknown genre or invalid artist credit.");
            }

            if (album.Tracks.Select(track => track.TrackNumber).Distinct().Count() != album.Tracks.Count)
            {
                throw new InvalidDataException($"Album '{album.Title}' has duplicate track numbers.");
            }
        }
    }
}
