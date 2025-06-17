using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;
using MyWebApp.Models;
using System.Linq;
using Xunit;

public class PageTests
{
    [Fact]
    public void CanAddAndRetrievePage()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new ApplicationDbContext(options))
        {
            context.Database.EnsureCreated();
            context.Pages.Add(new Page { Slug = "test", Title = "Test", Layout = "single-column" });
            context.SaveChanges();
        }

        using (var context = new ApplicationDbContext(options))
        {
            var page = context.Pages.Single(p => p.Slug == "test");
            Assert.Equal("Test", page.Title);
        }
    }
}
