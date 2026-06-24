using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Engine.Models;
using Microsoft.EntityFrameworkCore;

namespace Client.Engine.Services
{
    public class AgentDbContext : DbContext
    {
        public DbSet<JwtToken> JwtTokens { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public AgentDbContext(DbContextOptions<AgentDbContext> options) : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Account>(entity =>
            {
                entity.HasOne(t => t.JwtToken)
                    .WithOne(t => t.Account)
                    .HasForeignKey<JwtToken>(t => t.AccountId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}