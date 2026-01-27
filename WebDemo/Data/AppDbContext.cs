using Microsoft.EntityFrameworkCore;
using WebDemo.Models;

namespace WebDemo.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Models.Folder> Folders { get; set; }
        public DbSet<Models.File> Files { get; set; }
        public DbSet<Models.User> Users { get; set; }

    }
}
