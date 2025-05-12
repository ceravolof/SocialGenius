using SocialGenius.Models;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace SocialGenius.Data
{
    
    public class AppDbContext : IdentityDbContext<IdentityUser> //:DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Post> Posts { get; set; }
        public DbSet<User> EleUser { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configura esplicitamente la tabella Posts
            modelBuilder.Entity<Post>(entity =>
            {
                entity.ToTable("Posts");
                entity.HasKey(e => e.IdPost);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.DateCreate).HasDefaultValueSql("GETUTCDATE()");
            });
        }
    }
}
