using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;
using MyWebApp.Controllers;
using MyWebApp.Data;
using MyWebApp.Models;
using MyWebApp.Services;
using Xunit;

public class SanitizationTests
{
    private static (ApplicationDbContext ctx, LayoutService layout, HtmlSanitizerService sanitizer) CreateServices()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var ctx = new ApplicationDbContext(options);
        ctx.Database.EnsureCreated();
        var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new CacheService(memory);
        var layout = new LayoutService(cache);
        var sanitizer = new HtmlSanitizerService();
        return (ctx, layout, sanitizer);
    }

    [Fact]
    public async Task CreatePage_SanitizesHtml()
    {
        var (ctx, layout, sanitizer) = CreateServices();
        var controller = new AdminContentController(ctx, layout, sanitizer);
        var model = new Page
        {
            Slug = "test",
            Title = "Test",
            HeaderHtml = "<script>alert(1)</script><p>h</p>",
            BodyHtml = "<p>b</p><script>alert(2)</script>",
            FooterHtml = "<script>alert(3)</script>f"
        };
        var result = await controller.Create(model);
        Assert.IsType<RedirectToActionResult>(result);
        var page = ctx.Pages.Single(p => p.Slug == "test");
        Assert.DoesNotContain("<script", page.HeaderHtml, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", page.BodyHtml, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", page.FooterHtml, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSection_SanitizesHtml()
    {
        var (ctx, layout, sanitizer) = CreateServices();
        var controller = new AdminPageSectionController(ctx, layout, sanitizer);
        var model = new PageSection { PageId = ctx.Pages.First().Id, Area = "test", Html = "<div>hi</div><script>bad()</script>" };
        var result = await controller.Create(model);
        Assert.IsType<RedirectToActionResult>(result);
        var section = ctx.PageSections.First();
        Assert.DoesNotContain("<script", section.Html, System.StringComparison.OrdinalIgnoreCase);
    }
}
