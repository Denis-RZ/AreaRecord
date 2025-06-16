using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
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
            Layout = "single-column",
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
 
        var model = new PageSection { PageId = ctx.Pages.First().Id, Area = "test", Html = "<div>hi</div><script>bad()</script>", Type = PageSectionType.Html };
        var result = await controller.Create(model, null);
 
        Assert.IsType<RedirectToActionResult>(result);
        var section = ctx.PageSections.First();
        Assert.DoesNotContain("<script", section.Html, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditPage_SanitizesHtml()
    {
        var (ctx, layout, sanitizer) = CreateServices();
        var controller = new AdminContentController(ctx, layout, sanitizer);
        var page = ctx.Pages.First();
        page.HeaderHtml = "<script>alert(1)</script><p>h</p>";
        page.BodyHtml = "<p>b</p><script>alert(2)</script>";
        page.FooterHtml = "<script>alert(3)</script>f";
        var result = await controller.Edit(page);
        Assert.IsType<RedirectToActionResult>(result);
        var updated = ctx.Pages.Single(p => p.Id == page.Id);
        Assert.DoesNotContain("<script", updated.HeaderHtml, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", updated.BodyHtml, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", updated.FooterHtml, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSection_MarkdownConverted()
    {
        var (ctx, layout, sanitizer) = CreateServices();
        var controller = new AdminPageSectionController(ctx, layout, sanitizer);
        var model = new PageSection { PageId = ctx.Pages.First().Id, Area = "md", Html = "# Hello\n<script>bad()</script>", Type = PageSectionType.Markdown };
        var result = await controller.Create(model, null);
        Assert.IsType<RedirectToActionResult>(result);
        var section = ctx.PageSections.First(s => s.Area == "md");
        Assert.Contains("<h1>", section.Html);
        Assert.DoesNotContain("<script", section.Html, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSection_CodeEncoded()
    {
        var (ctx, layout, sanitizer) = CreateServices();
        var controller = new AdminPageSectionController(ctx, layout, sanitizer);
        var model = new PageSection { PageId = ctx.Pages.First().Id, Area = "code", Html = "<b>test</b>", Type = PageSectionType.Code };
        var result = await controller.Create(model, null);
        Assert.IsType<RedirectToActionResult>(result);
        var section = ctx.PageSections.First(s => s.Area == "code");
        Assert.Contains("&lt;b&gt;test&lt;/b&gt;", section.Html);
    }

    [Fact]
    public async Task CreateSection_ImageStoresTag()
    {
        var (ctx, layout, sanitizer) = CreateServices();
        var controller = new AdminPageSectionController(ctx, layout, sanitizer);
        var bytes = new byte[] {1,2,3};
        using var stream = new System.IO.MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "img.png");
        var model = new PageSection { PageId = ctx.Pages.First().Id, Area = "img", Type = PageSectionType.Image };
        var result = await controller.Create(model, file);
        Assert.IsType<RedirectToActionResult>(result);
        var section = ctx.PageSections.First(s => s.Area == "img");
        Assert.Contains("<img", section.Html);
    }
}
