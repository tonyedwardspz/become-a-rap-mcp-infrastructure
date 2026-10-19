using BecomeARapMcp.Api.Contracts;
using BecomeARapMcp.Api.Data;
using BecomeARapMcp.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BecomeARapMcp.Api.Endpoints;

public static class MusicEndpoints
{
    private const int DefaultPageSize = 50;
    private const int MaximumPageSize = 100;

    public static IEndpointRouteBuilder MapMusicEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var artists = endpoints.MapGroup("/artists").WithTags("Artists");
        artists.MapGet("/", GetArtists)
            .WithSummary("List artists")
            .WithDescription("Searches by artist name and returns a paginated result.");
        artists.MapGet("/{id:int}", GetArtist)
            .WithSummary("Get an artist")
            .WithDescription("Returns an artist and their albums.")
            .Produces<ArtistDetailDto>()
            .Produces(StatusCodes.Status404NotFound);

        var albums = endpoints.MapGroup("/albums").WithTags("Albums");
        albums.MapGet("/", GetAlbums)
            .WithSummary("List albums")
            .WithDescription("Returns a paginated album list with its primary artist.");
        albums.MapGet("/{id:int}", GetAlbum)
            .WithSummary("Get an album")
            .WithDescription("Returns an album, its tracks, genres, and credited artists.")
            .Produces<AlbumDetailDto>()
            .Produces(StatusCodes.Status404NotFound);

        var songs = endpoints.MapGroup("/songs").WithTags("Songs");
        songs.MapGet("/", GetSongs)
            .WithSummary("List and filter songs")
            .WithDescription("Searches song titles and artist names; optionally filters by genreId and/or artistId. Results include album, genre, primary, and featured artists.");
        songs.MapGet("/{id:int}", GetSong)
            .WithSummary("Get a song")
            .WithDescription("Returns a song with its album, genre, and primary and featured artists.")
            .Produces<SongDto>()
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapGet("/genres", GetGenres)
            .WithTags("Genres")
            .WithSummary("List genres")
            .WithDescription("Returns a paginated genre list with song counts.");

        return endpoints;
    }

    private static async Task<PagedResponse<ArtistDto>> GetArtists(
        MusicDbContext db,
        string? search,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.Artists.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(artist => EF.Functions.Like(artist.Name, pattern));
        }

        var paging = NormalizePaging(page, pageSize);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(artist => artist.Name)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(artist => new ArtistDto(
                artist.ArtistId,
                artist.Name,
                artist.Biography,
                artist.Country,
                artist.FormedYear))
            .ToListAsync(cancellationToken);

        return new PagedResponse<ArtistDto>(paging.Page, paging.PageSize, totalCount, items);
    }

    private static async Task<IResult> GetArtist(
        int id,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var artist = await db.Artists
            .AsNoTracking()
            .Include(item => item.Albums)
            .SingleOrDefaultAsync(item => item.ArtistId == id, cancellationToken);

        if (artist is null)
        {
            return Results.NotFound();
        }

        var albums = artist.Albums
            .OrderBy(album => album.ReleaseDate)
            .ThenBy(album => album.Title)
            .Select(album => new AlbumSummaryDto(
                album.AlbumId,
                album.Title,
                album.ReleaseDate,
                album.AlbumType,
                artist.ArtistId,
                artist.Name))
            .ToList();

        var result = new ArtistDetailDto(
            artist.ArtistId,
            artist.Name,
            artist.Biography,
            artist.Country,
            artist.FormedYear,
            albums);

        return Results.Ok(result);
    }

    private static async Task<PagedResponse<AlbumSummaryDto>> GetAlbums(
        MusicDbContext db,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.Albums.AsNoTracking();
        var paging = NormalizePaging(page, pageSize);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(album => album.Title)
            .ThenBy(album => album.AlbumId)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(album => new AlbumSummaryDto(
                album.AlbumId,
                album.Title,
                album.ReleaseDate,
                album.AlbumType,
                album.ArtistId,
                album.Artist.Name))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AlbumSummaryDto>(paging.Page, paging.PageSize, totalCount, items);
    }

    private static async Task<IResult> GetAlbum(
        int id,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var album = await db.Albums
            .AsNoTracking()
            .Where(item => item.AlbumId == id)
            .Include(item => item.Artist)
            .Include(item => item.Songs)
                .ThenInclude(song => song.Genre)
            .Include(item => item.Songs)
                .ThenInclude(song => song.SongArtists)
                    .ThenInclude(songArtist => songArtist.Artist)
            .SingleOrDefaultAsync(cancellationToken);

        if (album is null)
        {
            return Results.NotFound();
        }

        var songs = album.Songs
            .OrderBy(song => song.TrackNumber)
            .Select(song => ToSongDto(song, album))
            .ToList();

        return Results.Ok(new AlbumDetailDto(
            album.AlbumId,
            album.Title,
            album.ReleaseDate,
            album.AlbumType,
            album.ArtistId,
            album.Artist.Name,
            songs));
    }

    private static async Task<PagedResponse<SongDto>> GetSongs(
        MusicDbContext db,
        string? search,
        int? genreId,
        int? artistId,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.Songs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(song =>
                EF.Functions.Like(song.Title, pattern)
                || EF.Functions.Like(song.Album.Title, pattern)
                || song.SongArtists.Any(songArtist => EF.Functions.Like(songArtist.Artist.Name, pattern)));
        }

        if (genreId.HasValue)
        {
            query = query.Where(song => song.GenreId == genreId.Value);
        }

        if (artistId.HasValue)
        {
            query = query.Where(song => song.SongArtists.Any(songArtist => songArtist.ArtistId == artistId.Value));
        }

        var paging = NormalizePaging(page, pageSize);
        var totalCount = await query.CountAsync(cancellationToken);
        var songs = await query
            .OrderBy(song => song.Album.Title)
            .ThenBy(song => song.TrackNumber)
            .ThenBy(song => song.SongId)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Include(song => song.Album)
            .Include(song => song.Genre)
            .Include(song => song.SongArtists)
                .ThenInclude(songArtist => songArtist.Artist)
            .ToListAsync(cancellationToken);

        var items = songs.Select(ToSongDto).ToList();
        return new PagedResponse<SongDto>(paging.Page, paging.PageSize, totalCount, items);
    }

    private static async Task<IResult> GetSong(
        int id,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var song = await db.Songs
            .AsNoTracking()
            .Where(item => item.SongId == id)
            .Include(item => item.Album)
            .Include(item => item.Genre)
            .Include(item => item.SongArtists)
                .ThenInclude(songArtist => songArtist.Artist)
            .SingleOrDefaultAsync(cancellationToken);

        return song is null ? Results.NotFound() : Results.Ok(ToSongDto(song));
    }

    private static async Task<PagedResponse<GenreDto>> GetGenres(
        MusicDbContext db,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.Genres.AsNoTracking();
        var paging = NormalizePaging(page, pageSize);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(genre => genre.Name)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(genre => new GenreDto(
                genre.GenreId,
                genre.Name,
                genre.Description,
                genre.Songs.Count))
            .ToListAsync(cancellationToken);

        return new PagedResponse<GenreDto>(paging.Page, paging.PageSize, totalCount, items);
    }

    private static SongDto ToSongDto(Song song)
    {
        return ToSongDto(song, song.Album.Title, song.Album.ReleaseDate);
    }

    private static SongDto ToSongDto(Song song, Album album)
    {
        return ToSongDto(song, album.Title, album.ReleaseDate);
    }

    private static SongDto ToSongDto(Song song, string albumTitle, DateOnly? albumReleaseDate)
    {
        return new SongDto(
            song.SongId,
            song.Title,
            song.DurationSeconds,
            song.TrackNumber,
            song.Explicit,
            song.AlbumId,
            albumTitle,
            albumReleaseDate,
            song.GenreId,
            song.Genre.Name,
            song.SongArtists
                .OrderBy(songArtist => songArtist.Role == SongArtistRoles.Primary ? 0 : 1)
                .ThenBy(songArtist => songArtist.Artist.Name)
                .Select(songArtist => new SongArtistDto(
                    songArtist.ArtistId,
                    songArtist.Artist.Name,
                    songArtist.Role))
                .ToList());
    }

    private static (int Page, int PageSize, int Skip) NormalizePaging(int? page, int? pageSize)
    {
        var currentPage = Math.Max(1, page ?? 1);
        var currentPageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaximumPageSize);
        var skip = (int)Math.Min((long)(currentPage - 1) * currentPageSize, int.MaxValue);
        return (currentPage, currentPageSize, skip);
    }
}
