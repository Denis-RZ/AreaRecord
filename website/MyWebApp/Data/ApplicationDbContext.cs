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
        public DbSet<DownloadFile> DownloadFiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Download>()
                .HasIndex(d => d.DownloadTime);
            modelBuilder.Entity<Download>()
                .HasIndex(d => d.IsSuccessful);
            modelBuilder.Entity<Download>()
                .HasIndex(d => d.UserIP);
            modelBuilder.Entity<Download>()
                .HasIndex(d => d.Country);
            modelBuilder.Entity<Download>()
                .HasIndex(d => new { d.IsSuccessful, d.DownloadTime });
            modelBuilder.Entity<DownloadFile>()
                .HasIndex(f => f.FileName);
            modelBuilder.Entity<Recording>()
                .HasIndex(r => r.Created);

            // provider specific optimizations
            var provider = Database.ProviderName ?? string.Empty;
            if (provider.Contains("Npgsql"))
            {
                modelBuilder.Entity<Download>()
                    .Property(d => d.UserIP)
                    .HasColumnType("varchar(45)");
            }
            else if (provider.Contains("SqlServer"))
            {
                modelBuilder.Entity<Download>()
                    .Property(d => d.UserIP)
                    .HasColumnType("nvarchar(45)");
            }
        }
    }
}
