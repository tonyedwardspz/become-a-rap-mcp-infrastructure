namespace BecomeARapMcp.Api.Data.Seeding;

internal static class CatalogSeedValidator
{
    internal static void Validate(CatalogSeedData seed)
    {
        if (seed.Artists.Count == 0 || seed.Genres.Count == 0 || seed.Albums.Count == 0)
        {
            throw new InvalidDataException("The catalogue seed must contain artists, genres, and albums.");
        }

        foreach (var album in seed.Albums)
        {
            ValidateAlbum(seed, album);
        }
    }

    private static void ValidateAlbum(CatalogSeedData seed, SeedAlbum album)
    {
        ValidateSourceUrls(album);
        ValidateAlbumType(album);

        if (!seed.Artists.Any(artist =>
                string.Equals(artist.Name, album.Artist, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException($"Album '{album.Title}' refers to unknown artist '{album.Artist}'.");
        }

        ValidateTracks(seed.Genres, album);
    }

    private static void ValidateSourceUrls(SeedAlbum album)
    {
        ValidateHttpsUrl(album.SourceUrl, album.Title, "release");
        ValidateOptionalHttpsUrl(album.ExplicitSourceUrl, album.Title, "explicit-metadata");
        ValidateOptionalHttpsUrl(album.AlternateReleaseDateSourceUrl, album.Title, "alternate release-date");
    }

    private static void ValidateHttpsUrl(Uri uri, string albumTitle, string sourceName)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException($"Album '{albumTitle}' has an invalid {sourceName} source URL.");
        }
    }

    private static void ValidateOptionalHttpsUrl(Uri? uri, string albumTitle, string sourceName)
    {
        if (uri is not null)
        {
            ValidateHttpsUrl(uri, albumTitle, sourceName);
        }
    }

    private static void ValidateAlbumType(SeedAlbum album)
    {
        if (!string.Equals(album.AlbumType, "Album", StringComparison.Ordinal)
            && !string.Equals(album.AlbumType, "EP", StringComparison.Ordinal)
            && !string.Equals(album.AlbumType, "Single", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Album '{album.Title}' has unsupported type '{album.AlbumType}'.");
        }
    }

    private static void ValidateTracks(IReadOnlyCollection<SeedGenre> genres, SeedAlbum album)
    {
        if (album.Tracks.Count == 0)
        {
            throw new InvalidDataException($"Album '{album.Title}' has no track listing.");
        }

        if (album.Tracks.Any(IsInvalidTrack))
        {
            throw new InvalidDataException($"Album '{album.Title}' has an invalid track entry.");
        }

        if (album.Tracks.Any(track => HasInvalidCreditOrGenre(genres, track)))
        {
            throw new InvalidDataException(
                $"Album '{album.Title}' has a track with an unknown genre or invalid artist credit.");
        }

        if (album.Tracks.Select(track => track.TrackNumber).Distinct().Count() != album.Tracks.Count)
        {
            throw new InvalidDataException($"Album '{album.Title}' has duplicate track numbers.");
        }
    }

    private static bool IsInvalidTrack(SeedTrack track)
    {
        return track.TrackNumber < 1 || track.DurationSeconds < 0 || string.IsNullOrWhiteSpace(track.Title);
    }

    private static bool HasInvalidCreditOrGenre(IReadOnlyCollection<SeedGenre> genres, SeedTrack track)
    {
        return !genres.Any(genre => string.Equals(genre.Name, track.Genre, StringComparison.OrdinalIgnoreCase))
            || track.FeaturedArtists.Any(string.IsNullOrWhiteSpace);
    }
}
