using Microsoft.EntityFrameworkCore;
using Server.Models.Db.Identity;

namespace Server.Modules
{
    public class DataGuardDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DataGuardDbContext(DbContextOptions<DataGuardDbContext> options) : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(e => e.ToTable("identity"));
            modelBuilder.Entity<User>()
                .Property(e=>e.CreationTime)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            modelBuilder.Entity<User>()
                .Property(e => e.RefreshToken)
                .IsRequired(false);

            modelBuilder.Entity<UserGroup>(e => e.ToTable("identity"));
            modelBuilder.Entity<UserGroup>()
                .HasKey(e => new { e.UserId, e.GroupId });
            modelBuilder.Entity<UserGroup>().
                Property(e => e.JoinedAt)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
