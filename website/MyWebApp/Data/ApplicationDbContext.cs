using Microsoft.EntityFrameworkCore;
using MyWebApp.Models;

namespace MyWebApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Add DbSet<> properties here
        public DbSet<Recording> Recordings { get; set; }
        public DbSet<Download> Downloads { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Download>()
                .HasIndex(d => d.DownloadTime);
            modelBuilder.Entity<Download>()
                .HasIndex(d => d.IsSuccessful);
            modelBuilder.Entity<Download>()
                .HasIndex(d => d.UserIP);
        }
    }
}
