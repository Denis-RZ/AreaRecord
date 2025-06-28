using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using MyWebApp.Data;
using MyWebApp.Models;
using MyWebApp.Services;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

public class NavigationTests
{
    [Fact]
    public async Task PublishingPage_ShowsTitleOnceInHeader()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new CacheService(memory);
        var accessor = new HttpContextAccessor();
        var tokens = new TokenRenderService(accessor);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"Layouts:single-column:0", "main"},
                {"Layouts:two-column-sidebar:0", "main"},
                {"Layouts:two-column-sidebar:1", "sidebar"}
            })
            .Build();
        var layout = new LayoutService(cache, tokens, accessor);

        context.Pages.Add(new Page { Slug = "about", Title = "About", Layout = "single-column", IsPublished = true });
        context.SaveChanges();

        var html = await layout.GetHeaderAsync(context);

        Assert.Contains("About", html);
        Assert.Single(Regex.Matches(html, "About"));
    }
}
