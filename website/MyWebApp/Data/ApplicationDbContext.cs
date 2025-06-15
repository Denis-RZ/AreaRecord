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
        public DbSet<Page> Pages { get; set; }
        public DbSet<PageSection> PageSections { get; set; }
        public DbSet<AdminCredential> AdminCredentials { get; set; }
        public DbSet<Media> MediaItems { get; set; }

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
            modelBuilder.Entity<Media>()
                .HasIndex(m => m.FileName);
            modelBuilder.Entity<Recording>()
                .HasIndex(r => r.Created);

            modelBuilder.Entity<Page>()
                .HasIndex(p => p.Slug)
                .IsUnique();

            modelBuilder.Entity<PageSection>()
                .HasIndex(s => new { s.PageId, s.Area })
                .IsUnique();

            modelBuilder.Entity<Page>().HasData(
                new Page
                {
                    Id = 1,
                    Slug = "layout",
                    Title = "Layout",
                    HeaderHtml = "<div class=\"container-fluid nav-container\"><a class=\"logo\" href=\"/\">Screen Area Recorder Pro</a><nav class=\"site-nav\"><a href=\"/\">Home</a> <a href=\"/Download\">Download</a> <a href=\"/Home/Faq\">FAQ</a> <a href=\"/Home/Privacy\">Privacy</a> <a href=\"/Setup\">Setup</a> <a href=\"/Account/Login\">Login</a></nav></div>",
                    FooterHtml = "<div class=\"container\">&copy; 2025 - Screen Area Recorder Pro</div>"
                },
                new Page
                {
                    Id = 2,
                    Slug = "home",
                    Title = "Home",
                    BodyHtml = "<p>Welcome to Screen Area Recorder Pro.</p>"
                });

            modelBuilder.Entity<PageSection>().HasData(
                new PageSection
                {
                    Id = 1,
                    PageId = 1,
                    Area = "header",
                    Html = "<div class=\"container-fluid nav-container\"><a class=\"logo\" href=\"/\">Screen Area Recorder Pro</a><nav class=\"site-nav\"><a href=\"/\">Home</a> <a href=\"/Download\">Download</a> <a href=\"/Home/Faq\">FAQ</a> <a href=\"/Home/Privacy\">Privacy</a> <a href=\"/Setup\">Setup</a> <a href=\"/Account/Login\">Login</a></nav></div>"
                },
                new PageSection
                {
                    Id = 2,
                    PageId = 1,
                    Area = "footer",
                    Html = "<div class=\"container\">&copy; 2025 - Screen Area Recorder Pro</div>"
                });

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
