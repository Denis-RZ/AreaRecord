using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;
using MyWebApp.Models;
using MyWebApp.Services;
using Xunit;
using Microsoft.Data.Sqlite;

public class SchemaValidatorTests
{
    [Fact]
    public void Validate_ReturnsSuccess_ForDefaultContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options;
        using var context = new ApplicationDbContext(options);
        var validator = new SchemaValidator(context);
        var result = validator.Validate();
        Assert.True(result.Success);
        Assert.Empty(result.Messages);
    }

    private class NoIndexContext : ApplicationDbContext
    {
        public NoIndexContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var index in entity.GetIndexes().ToList())
                {
                    entity.RemoveIndex(index);
                }
            }
        }
    }

    [Fact]
    public void Validate_DetectsMissingIndexes()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = new NoIndexContext(options);
        context.Database.EnsureCreated();
        context.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS IX_Downloads_DownloadTime");
        var validator = new SchemaValidator(context);
        var result = validator.Validate();
        Assert.False(result.Success);
        Assert.Contains(result.Messages, m => m.Contains("DownloadTime"));
    }

    private class WrongColumnTypeContext : ApplicationDbContext
    {
        public WrongColumnTypeContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Download>()
                .Property(d => d.UserIP)
                .HasColumnType("text");
        }
    }

    [Fact]
    public void Validate_DetectsWrongColumnType()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options;
        using var context = new WrongColumnTypeContext(options);
        var validator = new SchemaValidator(context);
        var result = validator.Validate();
        Assert.False(result.Success);
        Assert.Contains("varchar(45)", string.Join(';', result.Messages));
    }
}
