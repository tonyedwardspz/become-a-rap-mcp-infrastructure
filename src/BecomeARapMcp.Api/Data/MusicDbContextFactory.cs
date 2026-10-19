using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BecomeARapMcp.Api.Data;

public sealed class MusicDbContextFactory : IDesignTimeDbContextFactory<MusicDbContext>
{
    public MusicDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MusicDbContext>()
            .UseSqlite("Data Source=music-catalogue-design.db")
            .Options;

        return new MusicDbContext(options);
    }
}
