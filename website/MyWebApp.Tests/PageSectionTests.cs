using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;
using MyWebApp.Models;
using Xunit;

public class PageSectionTests
{
    [Fact]
    public void CanAddAndRetrievePageSection()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new ApplicationDbContext(options))
        {
            context.Database.EnsureCreated();
            var page = new Page { Slug = "test", Title = "Test", Layout = "single-column" };
            context.Pages.Add(page);
            context.SaveChanges();
 
            context.PageSections.Add(new PageSection { PageId = page.Id, Area = "header", Html = "<p>hi</p>", Type = PageSectionType.Html });
 
            context.SaveChanges();
        }

        using (var context = new ApplicationDbContext(options))
        {
            var section = context.PageSections.Include(s => s.Page)
                .Single(s => s.Area == "header" && s.Page!.Slug == "test");
            Assert.Equal("<p>hi</p>", section.Html);
            Assert.Equal("test", section.Page!.Slug);
        }
    }
}
