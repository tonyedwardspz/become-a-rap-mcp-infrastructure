using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using BecomeARapMcp.Api.Contracts;
using BecomeARapMcp.Api.Data;
using BecomeARapMcp.Api.Data.Entities;
using BecomeARapMcp.Api.Data.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

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
        Assert.Equal(50, await db.Albums.CountAsync());
        Assert.Equal(756, await db.Songs.CountAsync());
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

        var stan = await db.Songs
            .Include(song => song.SongArtists)
                .ThenInclude(link => link.Artist)
            .SingleAsync(song => song.Title == "Stan");
        var eminemCredit = stan.SongArtists.Single(link => link.Artist.Name == "Eminem");
        eminemCredit.Role = SongArtistRoles.Featured;
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<MusicCatalogSeeder>()
            .SeedAsync(db, CancellationToken.None);

        Assert.Equal(
            SongArtistRoles.Primary,
            await db.SongArtists
                .Where(link => link.SongId == stan.SongId && link.ArtistId == eminemCredit.ArtistId)
                .Select(link => link.Role)
                .SingleAsync());
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
        Assert.Equal(50, albumPage.TotalCount);

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
        Assert.Equal(756, genre.SongCount);

        var byGenre = await _client.GetFromJsonAsync<PagedResponse<SongDto>>($"/songs?genreId={genre.GenreId}&pageSize=100");
        Assert.Equal(756, Assert.IsType<PagedResponse<SongDto>>(byGenre).TotalCount);
    }

    [Fact]
    public async Task CollectionEndpointsReturnStablePagingAndRespectPageSizeBounds()
    {
        var firstPage = await _client.GetFromJsonAsync<PagedResponse<AlbumSummaryDto>>("/albums?page=1&pageSize=2");
        var secondPage = await _client.GetFromJsonAsync<PagedResponse<AlbumSummaryDto>>("/albums?page=2&pageSize=2");
        var oversizedPage = await _client.GetFromJsonAsync<PagedResponse<AlbumSummaryDto>>("/albums?pageSize=1000");
        var zeroSizedPage = await _client.GetFromJsonAsync<PagedResponse<AlbumSummaryDto>>("/albums?pageSize=0");
        var negativePage = await _client.GetFromJsonAsync<PagedResponse<AlbumSummaryDto>>("/albums?page=-1");
        var beyondLastPage = await _client.GetFromJsonAsync<PagedResponse<AlbumSummaryDto>>("/albums?page=999&pageSize=2");
        var defaultPage = await _client.GetFromJsonAsync<PagedResponse<AlbumSummaryDto>>("/albums");

        var first = Assert.IsType<PagedResponse<AlbumSummaryDto>>(firstPage);
        var second = Assert.IsType<PagedResponse<AlbumSummaryDto>>(secondPage);
        var oversized = Assert.IsType<PagedResponse<AlbumSummaryDto>>(oversizedPage);
        var zeroSized = Assert.IsType<PagedResponse<AlbumSummaryDto>>(zeroSizedPage);
        var negative = Assert.IsType<PagedResponse<AlbumSummaryDto>>(negativePage);
        var beyondLast = Assert.IsType<PagedResponse<AlbumSummaryDto>>(beyondLastPage);
        var defaults = Assert.IsType<PagedResponse<AlbumSummaryDto>>(defaultPage);

        Assert.Equal(1, first.Page);
        Assert.Equal(2, first.PageSize);
        Assert.Equal(2, first.Items.Count);
        Assert.Equal(2, second.Page);
        Assert.Equal(2, second.PageSize);
        Assert.Equal(2, second.Items.Count);
        Assert.Empty(first.Items.Select(album => album.AlbumId).Intersect(second.Items.Select(album => album.AlbumId)));
        Assert.Equal(first.Items.OrderBy(album => album.Title).Select(album => album.AlbumId), first.Items.Select(album => album.AlbumId));
        Assert.Equal(100, oversized.PageSize);
        Assert.Equal(50, oversized.Items.Count);
        Assert.Equal(1, zeroSized.PageSize);
        Assert.Single(zeroSized.Items);
        Assert.Equal(1, negative.Page);
        Assert.Equal(50, defaults.PageSize);
        Assert.Equal(50, defaults.Items.Count);
        Assert.Equal(999, beyondLast.Page);
        Assert.Empty(beyondLast.Items);
    }

    [Fact]
    public async Task SongSearchMatchesSongAlbumAndArtistNamesCaseInsensitively()
    {
        var bySongTitle = await _client.GetFromJsonAsync<PagedResponse<SongDto>>("/songs?search=stan");
        var byAlbumTitle = await _client.GetFromJsonAsync<PagedResponse<SongDto>>("/songs?search=marshall%20mathers");
        var byArtistName = await _client.GetFromJsonAsync<PagedResponse<SongDto>>("/songs?search=dido");

        Assert.Contains(Assert.IsType<PagedResponse<SongDto>>(bySongTitle).Items, song => song.Title == "Stan");
        Assert.Contains(Assert.IsType<PagedResponse<SongDto>>(byAlbumTitle).Items, song => song.AlbumTitle == "The Marshall Mathers LP");
        Assert.Contains(Assert.IsType<PagedResponse<SongDto>>(byArtistName).Items, song =>
            song.Title == "Stan" && song.Artists.Any(artist => artist.Name == "Dido"));
    }

    [Theory]
    [InlineData("/artists/999999")]
    [InlineData("/albums/999999")]
    [InlineData("/songs/999999")]
    public async Task DetailEndpointsReturnNotFoundForUnknownIds(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SongDetailReturnsTheSameRelationshipProjectionAsTheCollection()
    {
        var songs = await _client.GetFromJsonAsync<PagedResponse<SongDto>>("/songs?search=stan");
        var listItem = Assert.IsType<PagedResponse<SongDto>>(songs).Items
            .Single(song => song.Title == "Stan");
        var detail = await _client.GetFromJsonAsync<SongDto>($"/songs/{listItem.SongId}");
        var song = Assert.IsType<SongDto>(detail);

        Assert.Equal(listItem.SongId, song.SongId);
        Assert.Equal(listItem.Title, song.Title);
        Assert.Equal(listItem.AlbumId, song.AlbumId);
        Assert.Equal(listItem.AlbumTitle, song.AlbumTitle);
        Assert.Equal(listItem.GenreId, song.GenreId);
        Assert.Equal(listItem.GenreName, song.GenreName);
        Assert.Equal(listItem.Artists, song.Artists);
        Assert.Equal("The Marshall Mathers LP", song.AlbumTitle);
        Assert.Equal("Hip Hop", song.GenreName);
        Assert.Equal(
            new[] { "Eminem", "Dido" },
            song.Artists.Select(artist => artist.Name));
        Assert.Equal(SongArtistRoles.Primary, song.Artists[0].Role);
        Assert.Equal(SongArtistRoles.Featured, song.Artists[1].Role);
    }

    [Fact]
    public async Task CollectionEndpointsReturnEmptyPagesForNoMatchesAndUnknownFilters()
    {
        var noArtists = await _client.GetFromJsonAsync<PagedResponse<ArtistDto>>("/artists?search=no-such-artist");
        var noSongs = await _client.GetFromJsonAsync<PagedResponse<SongDto>>("/songs?search=no-such-song");
        var unknownArtist = await _client.GetFromJsonAsync<PagedResponse<SongDto>>("/songs?artistId=999999");
        var unknownGenre = await _client.GetFromJsonAsync<PagedResponse<SongDto>>("/songs?genreId=999999");

        Assert.Empty(Assert.IsType<PagedResponse<ArtistDto>>(noArtists).Items);
        Assert.Empty(Assert.IsType<PagedResponse<SongDto>>(noSongs).Items);
        Assert.Empty(Assert.IsType<PagedResponse<SongDto>>(unknownArtist).Items);
        Assert.Empty(Assert.IsType<PagedResponse<SongDto>>(unknownGenre).Items);
    }

    [Fact]
    public async Task CollectionEndpointsApplyAllFiltersAndReturnDeterministicOrdering()
    {
        var artists = await _client.GetFromJsonAsync<PagedResponse<ArtistDto>>("/artists?pageSize=100");
        var albums = await _client.GetFromJsonAsync<PagedResponse<AlbumSummaryDto>>("/albums?pageSize=100");
        var songs = await _client.GetFromJsonAsync<PagedResponse<SongDto>>("/songs?search=stan&artistId=1&genreId=1&pageSize=100");

        var artistItems = Assert.IsType<PagedResponse<ArtistDto>>(artists).Items;
        var albumItems = Assert.IsType<PagedResponse<AlbumSummaryDto>>(albums).Items;
        var songItems = Assert.IsType<PagedResponse<SongDto>>(songs).Items;

        Assert.Equal(artistItems.OrderBy(artist => artist.Name, StringComparer.Ordinal).Select(artist => artist.ArtistId), artistItems.Select(artist => artist.ArtistId));
        Assert.Equal(albumItems.OrderBy(album => album.Title, StringComparer.Ordinal).ThenBy(album => album.AlbumId).Select(album => album.AlbumId), albumItems.Select(album => album.AlbumId));
        Assert.Single(songItems);
        Assert.Equal("Stan", songItems[0].Title);
        Assert.Equal(1, songItems[0].GenreId);
        Assert.Contains(songItems[0].Artists, artist => artist.ArtistId == 1);
    }

    [Fact]
    public async Task CollectionEndpointsReturnJsonWithTheirPagingContract()
    {
        var response = await _client.GetAsync("/artists?pageSize=1");
        response.EnsureSuccessStatusCode();

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;
        Assert.True(body.TryGetProperty("page", out var page));
        Assert.True(body.TryGetProperty("pageSize", out var pageSize));
        Assert.True(body.TryGetProperty("totalCount", out var totalCount));
        Assert.True(body.TryGetProperty("items", out var items));
        Assert.Equal(1, page.GetInt32());
        Assert.Equal(1, pageSize.GetInt32());
        Assert.True(totalCount.GetInt32() > 0);
        Assert.Single(items.EnumerateArray());
    }

    [Fact]
    public async Task DetailEndpointsReturnOrderedRelatedResources()
    {
        var albums = await _client.GetFromJsonAsync<PagedResponse<AlbumSummaryDto>>("/albums?pageSize=100");
        var marshallMathers = Assert.IsType<PagedResponse<AlbumSummaryDto>>(albums).Items
            .Single(album => album.Title == "The Marshall Mathers LP");
        var album = await _client.GetFromJsonAsync<AlbumDetailDto>($"/albums/{marshallMathers.AlbumId}");
        var artist = await _client.GetFromJsonAsync<ArtistDetailDto>($"/artists/{marshallMathers.ArtistId}");

        var tracks = Assert.IsType<AlbumDetailDto>(album).Songs;
        var artistAlbums = Assert.IsType<ArtistDetailDto>(artist).Albums;

        Assert.Equal(tracks.OrderBy(song => song.TrackNumber).Select(song => song.SongId), tracks.Select(song => song.SongId));
        Assert.Equal(
            artistAlbums.OrderBy(item => item.ReleaseDate).ThenBy(item => item.Title).Select(item => item.AlbumId),
            artistAlbums.Select(item => item.AlbumId));
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

    [Theory]
    [MemberData(nameof(InvalidSeeds))]
    public async Task SeederRejectsInvalidSeedWithoutPersistingData(string seedJson)
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"invalid-catalogue-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(contentRoot, "catalogue.db");
        Directory.CreateDirectory(Path.Combine(contentRoot, "Data", "Seed"));

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(contentRoot, "Data", "Seed", "music-catalogue.json"),
                seedJson);

            var options = new DbContextOptionsBuilder<MusicDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            await using var db = new MusicDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var seeder = new MusicCatalogSeeder(new TestWebHostEnvironment(contentRoot), NullLogger<MusicCatalogSeeder>.Instance);

            await Assert.ThrowsAsync<InvalidDataException>(() => seeder.SeedAsync(db, CancellationToken.None));
            Assert.Empty(await db.Artists.ToListAsync());
            Assert.Empty(await db.Albums.ToListAsync());
            Assert.Empty(await db.Songs.ToListAsync());
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void DatabaseResetIsRejectedOutsideDevelopment()
    {
        using var resetFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseContentRoot(ApiContentRoot);
            builder.ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["CATALOGUE_RESET_DATABASE"] = "true",
                }));
        });

        var exception = Assert.Throws<InvalidOperationException>(() => resetFactory.CreateClient());

        Assert.Equal("Database reset is only available in the Development environment.", exception.Message);
    }

    [Fact]
    public async Task DatabaseRejectsUnsupportedAlbumTypesAndSongArtistRoles()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"catalogue-constraints-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<MusicDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            await using var db = new MusicDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var artist = new Artist { Name = "Artist" };
            var genre = new Genre { Name = "Hip Hop" };
            db.AddRange(artist, genre);
            await db.SaveChangesAsync();

            db.Albums.Add(new Album { ArtistId = artist.ArtistId, Title = "Invalid", AlbumType = "Mixtape" });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();

            var album = new Album { ArtistId = artist.ArtistId, Title = "Valid", AlbumType = "Album" };
            db.Albums.Add(album);
            await db.SaveChangesAsync();
            var song = new Song
            {
                AlbumId = album.AlbumId,
                Title = "Song",
                DurationSeconds = 120,
                TrackNumber = 1,
                GenreId = genre.GenreId,
            };
            db.Songs.Add(song);
            await db.SaveChangesAsync();

            db.SongArtists.Add(new SongArtist
            {
                SongId = song.SongId,
                ArtistId = artist.ArtistId,
                Role = "Guest",
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ConcurrentSeedingDoesNotCreateDuplicateRelationships()
    {
        async Task SeedInSeparateScopeAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MusicDbContext>();
            var seeder = scope.ServiceProvider.GetRequiredService<MusicCatalogSeeder>();
            await seeder.SeedAsync(db, CancellationToken.None);
        }

        await Task.WhenAll(SeedInSeparateScopeAsync(), SeedInSeparateScopeAsync());

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<MusicDbContext>();
        var duplicateLinks = await verificationDb.SongArtists
            .GroupBy(link => new { link.SongId, link.ArtistId })
            .CountAsync(group => group.Count() > 1);

        Assert.Equal(0, duplicateLinks);
    }

    private static string ApiContentRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../BecomeARapMcp.Api"));

    public static IEnumerable<object[]> InvalidSeeds =>
    [
        ["""
          {"artists":[{"name":"Artist"}],"genres":[{"name":"Hip Hop"}],"albums":[{"artist":"Artist","title":"Album","albumType":"Album","sourceUrl":"http://example.test","tracks":[{"trackNumber":1,"title":"Track","durationSeconds":120,"explicit":false,"genre":"Hip Hop"}]}]}
          """],
        ["""
          {"artists":[{"name":"Artist"}],"genres":[{"name":"Hip Hop"}],"albums":[{"artist":"Unknown","title":"Album","albumType":"Album","sourceUrl":"https://example.test","tracks":[{"trackNumber":1,"title":"Track","durationSeconds":120,"explicit":false,"genre":"Hip Hop"}]}]}
          """],
        ["""
          {"artists":[{"name":"Artist"}],"genres":[{"name":"Hip Hop"}],"albums":[{"artist":"Artist","title":"Album","albumType":"Album","sourceUrl":"https://example.test","tracks":[{"trackNumber":1,"title":"Track","durationSeconds":120,"explicit":false,"genre":"Unknown"}]}]}
          """],
        ["""
          {"artists":[{"name":"Artist"}],"genres":[{"name":"Hip Hop"}],"albums":[{"artist":"Artist","title":"Album","albumType":"Album","sourceUrl":"https://example.test","tracks":[{"trackNumber":1,"title":"Track One","durationSeconds":120,"explicit":false,"genre":"Hip Hop"},{"trackNumber":1,"title":"Track Two","durationSeconds":120,"explicit":false,"genre":"Hip Hop"}]}]}
          """],
        ["""
          {"artists":[{"name":"Artist"}],"genres":[{"name":"Hip Hop"}],"albums":[{"artist":"Artist","title":"Album","albumType":"Album","sourceUrl":"https://example.test","tracks":[{"trackNumber":1,"title":"Track","durationSeconds":-1,"explicit":false,"genre":"Hip Hop"}]}]}
          """],
        ["""
          {"artists":[],"genres":[{"name":"Hip Hop"}],"albums":[{"artist":"Artist","title":"Album","albumType":"Album","sourceUrl":"https://example.test","tracks":[{"trackNumber":1,"title":"Track","durationSeconds":120,"explicit":false,"genre":"Hip Hop"}]}]}
          """],
    ];

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = nameof(BecomeARapMcp);
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
