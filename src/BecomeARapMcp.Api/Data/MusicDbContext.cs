using BecomeARapMcp.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BecomeARapMcp.Api.Data;

public sealed class MusicDbContext(DbContextOptions<MusicDbContext> options) : DbContext(options)
{
    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<Song> Songs => Set<Song>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<SongArtist> SongArtists => Set<SongArtist>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Artist>(entity =>
        {
            entity.ToTable("Artists");
            entity.HasKey(artist => artist.ArtistId);
            entity.Property(artist => artist.Name).IsRequired();
            entity.HasIndex(artist => artist.Name);
        });

        modelBuilder.Entity<Album>(entity =>
        {
            entity.ToTable("Albums", table => table.HasCheckConstraint(
                "CK_Albums_AlbumType",
                "\"AlbumType\" IN ('Album', 'EP', 'Single')"));
            entity.HasKey(album => album.AlbumId);
            entity.Property(album => album.Title).IsRequired();
            entity.Property(album => album.AlbumType).IsRequired();
            entity.HasIndex(album => album.ArtistId);
            entity.HasIndex(album => album.ReleaseDate);
            entity.HasOne(album => album.Artist)
                .WithMany(artist => artist.Albums)
                .HasForeignKey(album => album.ArtistId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Song>(entity =>
        {
            entity.ToTable("Songs");
            entity.HasKey(song => song.SongId);
            entity.Property(song => song.Title).IsRequired();
            entity.HasIndex(song => song.AlbumId);
            entity.HasIndex(song => song.GenreId);
            entity.HasIndex(song => new { song.AlbumId, song.TrackNumber }).IsUnique();
            entity.HasOne(song => song.Album)
                .WithMany(album => album.Songs)
                .HasForeignKey(song => song.AlbumId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(song => song.Genre)
                .WithMany(genre => genre.Songs)
                .HasForeignKey(song => song.GenreId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.ToTable("Genres");
            entity.HasKey(genre => genre.GenreId);
            entity.Property(genre => genre.Name).IsRequired();
            entity.HasIndex(genre => genre.Name).IsUnique();
        });

        modelBuilder.Entity<SongArtist>(entity =>
        {
            entity.ToTable("SongArtists", table => table.HasCheckConstraint(
                "CK_SongArtists_Role",
                "\"Role\" IN ('Primary', 'Featured')"));
            entity.HasKey(songArtist => new { songArtist.SongId, songArtist.ArtistId });
            entity.Property(songArtist => songArtist.Role).IsRequired();
            entity.HasIndex(songArtist => songArtist.ArtistId);
            entity.HasOne(songArtist => songArtist.Song)
                .WithMany(song => song.SongArtists)
                .HasForeignKey(songArtist => songArtist.SongId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(songArtist => songArtist.Artist)
                .WithMany(artist => artist.SongArtists)
                .HasForeignKey(songArtist => songArtist.ArtistId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
