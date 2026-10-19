using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BecomeARapMcp.Api.Tests;

public sealed class MusicCatalogApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"music-catalogue-tests-{Guid.NewGuid():N}.db");
    private readonly string? _originalConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__sqlite");

    public MusicCatalogApiFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__sqlite", $"Data Source={_databasePath}");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var apiContentRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../BecomeARapMcp.Api"));

        builder.UseEnvironment("Development");
        builder.UseContentRoot(apiContentRoot);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        Environment.SetEnvironmentVariable("ConnectionStrings__sqlite", _originalConnectionString);
        foreach (var suffix in new[] { string.Empty, "-shm", "-wal" })
        {
            var path = _databasePath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
