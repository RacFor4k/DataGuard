using Microsoft.EntityFrameworkCore;
using Server.Models;
using Microsoft.Extensions.Logging;

namespace Server.Services
{
    public class DataGuardDbContext : DbContext
    {
        private readonly ILogger<DataGuardDbContext> _logger;
        public DbSet<User> Users { get; set; }
        public DbSet<UserJwt> UserJwtRefreshTokens { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<Icon> Icons { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DataGuardDbContext(DbContextOptions<DataGuardDbContext> options, ILogger<DataGuardDbContext> logger) : base(options)
        {
            _logger = logger;
        }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            _logger.LogTrace($"OnModelCreating called (peer: unknown)");
            base.OnModelCreating(modelBuilder);
            _logger.LogTrace($"OnModelCreating completed (peer: unknown)");

            modelBuilder.Entity<GroupMember>(entity =>
            {
                _logger.LogTrace($"Configuring GroupMember entity (peer: unknown)");
                entity.HasKey(e => new { e.GroupId, e.UserId,});

                entity.HasOne(e => e.Group)
                    .WithMany(g => g.Members)
                    .HasForeignKey(e => e.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.GroupMembers)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Role).HasConversion<string>();
                _logger.LogTrace($"GroupMember entity configured successfully (peer: unknown)");
            });

            modelBuilder.Entity<Group>(entity =>
            {
                _logger.LogTrace($"Configuring Group entity (peer: unknown)");
                entity.HasOne(g => g.Icon)
                    .WithMany()
                    .HasForeignKey(g => g.IconId)
                    .OnDelete(DeleteBehavior.SetNull);
                _logger.LogTrace($"Group entity configured successfully (peer: unknown)");
            });

            modelBuilder.Entity<Company>(entity =>
            {
                _logger.LogTrace($"Configuring Company entity (peer: unknown)");
                entity.HasMany(c => c.Users)
                    .WithOne(u => u.Company)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(c => c.Groups)
                    .WithOne(g => g.Company)
                    .OnDelete(DeleteBehavior.Cascade);
                _logger.LogTrace($"Company entity configured successfully (peer: unknown)");
            });
        }
    }
}