using Microsoft.EntityFrameworkCore;
using Server.Storage.Models;

namespace Server.Storage.Data;

public class StorageDbContext : DbContext
{
    public DbSet<StorageFile> Files => Set<StorageFile>();
    public DbSet<StorageDirectory> Directories => Set<StorageDirectory>();
    public DbSet<FileMetadataEntry> FileMetadataEntries => Set<FileMetadataEntry>();
    public DbSet<StorageFileAccess> FileAccesses => Set<StorageFileAccess>();
    public DbSet<StorageSharedLink> SharedLinks => Set<StorageSharedLink>();
    public DbSet<StorageNonce> Nonces => Set<StorageNonce>();

    public StorageDbContext(DbContextOptions<StorageDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StorageFile>(entity =>
        {
            entity.HasIndex(e => e.FileId);
            entity.HasIndex(e => new { e.OwnerId, e.NormalizedPath });
            entity.HasQueryFilter(e => e.DeletedAtUtc == null);
            entity.Property(e => e.ContentHash).HasColumnType("bytea");
        });

        modelBuilder.Entity<StorageDirectory>(entity =>
        {
            entity.HasIndex(e => e.DirectoryId);
            entity.HasIndex(e => new { e.OwnerId, e.NormalizedPath });
            entity.HasIndex(e => new { e.OwnerId, e.ParentDirectoryId, e.NormalizedName }).IsUnique();
            entity.HasQueryFilter(e => e.DeletedAtUtc == null);
        });

        modelBuilder.Entity<FileMetadataEntry>(entity =>
        {
            entity.HasIndex(e => e.FileId);
            entity.HasOne(e => e.File)
                .WithMany(f => f.Metadata)
                .HasForeignKey(e => e.FileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.File.DeletedAtUtc == null);
        });

        modelBuilder.Entity<StorageFileAccess>(entity =>
        {
            entity.HasIndex(e => e.FileId);
            entity.HasOne(e => e.File)
                .WithMany()
                .HasForeignKey(e => e.FileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.File.DeletedAtUtc == null);
        });

        modelBuilder.Entity<StorageSharedLink>(entity =>
        {
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasOne(e => e.File)
                .WithMany()
                .HasForeignKey(e => e.FileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.File.DeletedAtUtc == null);
        });

        modelBuilder.Entity<StorageNonce>(entity =>
        {
            entity.HasIndex(e => new { e.OwnerId, e.OperationName, e.Token });
        });
    }
}
