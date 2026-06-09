using Microsoft.EntityFrameworkCore;
using Server.Models;

namespace Server.Services
{
    public class DataGuardDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<UserJwt> UserJwtRefreshTokens { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<Icon> Icons { get; set; }
        public DbSet<Company> Companies { get; set; }
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

            modelBuilder.Entity<Company>(entity =>
            {
                entity.HasMany(c => c.Users)
                    .WithOne(u => u.Company)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(c => c.Groups)
                    .WithOne(g => g.Company)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}