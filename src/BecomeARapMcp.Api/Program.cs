using BecomeARapMcp.Api.Data;
using BecomeARapMcp.Api.Data.Seeding;
using BecomeARapMcp.Api.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSqliteConnection("sqlite");
var sqliteConnectionString = builder.Configuration.GetConnectionString("sqlite")
    ?? throw new InvalidOperationException("The Aspire 'sqlite' connection string was not configured.");
builder.Services.AddDbContext<MusicDbContext>(options => options.UseSqlite(sqliteConnectionString));
builder.Services.AddScoped<MusicCatalogSeeder>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await InitializeDatabaseAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/hello", () => TypedResults.Ok(new { message = "Hello" }))
    .WithName("Hello");

app.MapMusicEndpoints();
app.MapDefaultEndpoints();
app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    var resetRequested = bool.TryParse(app.Configuration["CATALOGUE_RESET_DATABASE"], out var requested) && requested;
    if (resetRequested && !app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("Database reset is only available in the Development environment.");
    }

    if (!app.Environment.IsDevelopment())
    {
        return;
    }

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<MusicDbContext>();
    await db.Database.MigrateAsync();

    if (resetRequested)
    {
        await db.SongArtists.ExecuteDeleteAsync();
        await db.Songs.ExecuteDeleteAsync();
        await db.Albums.ExecuteDeleteAsync();
        await db.Genres.ExecuteDeleteAsync();
        await db.Artists.ExecuteDeleteAsync();
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM sqlite_sequence WHERE name IN ('Artists', 'Albums', 'Songs', 'Genres')");
    }

    await scope.ServiceProvider.GetRequiredService<MusicCatalogSeeder>()
        .SeedAsync(db, CancellationToken.None);
}

public partial class Program { }
