using System.Net.Http.Json;
using BecomeARapMcp.Api.Contracts;
using BecomeARapMcp.Api.Data;
using BecomeARapMcp.Api.Data.Entities;
using BecomeARapMcp.Api.Data.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BecomeARapMcp.Api.Tests;

public sealed class MusicCatalogApiTests(MusicCatalogApiFactory factory) : IClassFixture<MusicCatalogApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task StartupMigratesAndSeedsRelationshipsIdempotently()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MusicDbContext>();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(5, await db.Albums.CountAsync());
        Assert.Equal(90, await db.Songs.CountAsync());
        Assert.Equal(1, await db.Genres.CountAsync());
        Assert.Equal(
            0,
            await db.Songs.CountAsync(song => !song.SongArtists.Any(link => link.Role == SongArtistRoles.Primary)));
        Assert.True(await db.SongArtists.AnyAsync(link =>
            link.Role == SongArtistRoles.Featured
            && link.Artist.Name == "Dido"
            && link.Song.Title == "Stan"));

        var before = await CountsAsync(db);
        await scope.ServiceProvider.GetRequiredService<MusicCatalogSeeder>()
            .SeedAsync(db, CancellationToken.None);
        Assert.Equal(before, await CountsAsync(db));
    }

    [Fact]
    public async Task SongSearchAndArtistFilterReturnAlbumGenreAndFeaturedArtists()
    {
        var artists = await _client.GetFromJsonAsync<PagedResponse<ArtistDto>>("/artists?search=Dido");
        var dido = Assert.Single(Assert.IsType<PagedResponse<ArtistDto>>(artists).Items);

        var songs = await _client.GetFromJsonAsync<PagedResponse<SongDto>>($"/songs?search=stan&artistId={dido.ArtistId}");
        var stan = Assert.Single(Assert.IsType<PagedResponse<SongDto>>(songs).Items);

        Assert.Equal("Stan", stan.Title);
        Assert.Equal("The Marshall Mathers LP", stan.AlbumTitle);
        Assert.Equal("Hip Hop", stan.GenreName);
        Assert.True(stan.Explicit);
        Assert.Contains(stan.Artists, artist => artist.Name == "Eminem" && artist.Role == SongArtistRoles.Primary);
        Assert.Contains(stan.Artists, artist => artist.Name == "Dido" && artist.Role == SongArtistRoles.Featured);

        var songDetails = await _client.GetFromJsonAsync<SongDto>($"/songs/{stan.SongId}");
        Assert.Equal(stan.SongId, Assert.IsType<SongDto>(songDetails).SongId);
    }

    [Fact]
    public async Task AlbumAndGenreEndpointsReturnRelatedData()
    {
        var albums = await _client.GetFromJsonAsync<PagedResponse<AlbumSummaryDto>>("/albums?pageSize=2");
        var albumPage = Assert.IsType<PagedResponse<AlbumSummaryDto>>(albums);
        Assert.Equal(2, albumPage.Items.Count);
        Assert.Equal(5, albumPage.TotalCount);

        var marshallMathers = albumPage.Items.FirstOrDefault(album => album.Title == "The Marshall Mathers LP")
            ?? (await _client.GetFromJsonAsync<PagedResponse<AlbumSummaryDto>>("/albums?pageSize=100"))!
                .Items.Single(album => album.Title == "The Marshall Mathers LP");
        var album = await _client.GetFromJsonAsync<AlbumDetailDto>($"/albums/{marshallMathers.AlbumId}");
        var albumDetails = Assert.IsType<AlbumDetailDto>(album);
        Assert.Equal(18, albumDetails.Songs.Count);
        Assert.Contains(albumDetails.Songs, song => song.Title == "Stan" && song.Artists.Any(artist => artist.Name == "Dido"));

        var artist = await _client.GetFromJsonAsync<ArtistDetailDto>($"/artists/{marshallMathers.ArtistId}");
        Assert.Contains(Assert.IsType<ArtistDetailDto>(artist).Albums, item => item.Title == "The Marshall Mathers LP");

        var genres = await _client.GetFromJsonAsync<PagedResponse<GenreDto>>("/genres");
        var genre = Assert.Single(Assert.IsType<PagedResponse<GenreDto>>(genres).Items);
        Assert.Equal("Hip Hop", genre.Name);
        Assert.Equal(90, genre.SongCount);

        var byGenre = await _client.GetFromJsonAsync<PagedResponse<SongDto>>($"/songs?genreId={genre.GenreId}&pageSize=100");
        Assert.Equal(90, Assert.IsType<PagedResponse<SongDto>>(byGenre).TotalCount);
    }

    private static async Task<(int Artists, int Albums, int Songs, int Genres, int SongArtists)> CountsAsync(
        MusicDbContext db)
    {
        return (
            await db.Artists.CountAsync(),
            await db.Albums.CountAsync(),
            await db.Songs.CountAsync(),
            await db.Genres.CountAsync(),
            await db.SongArtists.CountAsync());
    }
}
