using Microsoft.EntityFrameworkCore;
using Server.Models;

namespace Server.Services
{
    public class DataGuardDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<DbUserJwt> UserJwtRefreshTokens { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<Icon> Icons { get; set; }
        public DataGuardDbContext(DbContextOptions<DataGuardDbContext> options) : base(options){}
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<GroupMember>(entity =>
            {
                entity.HasKey(e => new { e.GroupId, e.UserId,});
                entity.HasOne<Group>()
                    .WithMany(g => g.Members)
                    .HasForeignKey(e => e.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<User>()
                    .WithMany(u => u.GroupMembers)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Role).HasConversion<string>();
            });

            modelBuilder.Entity<Group>(entity =>
            {
                entity.HasOne(g => g.Icon)
                    .WithMany()
                    .HasForeignKey(g => g.IconId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}