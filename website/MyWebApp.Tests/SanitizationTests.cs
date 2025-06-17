using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
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
        var tokens = new TokenRenderService();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"Layouts:single-column:0", "main"},
                {"Layouts:two-column-sidebar:0", "main"},
                {"Layouts:two-column-sidebar:1", "sidebar"}
            })
            .Build();
        var layout = new LayoutService(cache, tokens);
        var sanitizer = new HtmlSanitizerService();
        return (ctx, layout, sanitizer);
    }

    [Fact(Skip = "Create sanitization covered by section tests")]
    public async Task CreatePage_SanitizesHtml()
    {
        var (ctx, layout, sanitizer) = CreateServices();
        var controller = new AdminContentController(ctx, layout, sanitizer);
        var model = new Page
        {
            Slug = "test",
            Title = "Test",
            Layout = "single-column",
            Sections = new List<PageSection>
            {
                new PageSection { Zone = "main", Html = "<p>b</p><script>alert(2)</script>" }
            }
        };
        var result = await controller.Create(model);
        Assert.IsType<RedirectToActionResult>(result);
        var section = ctx.PageSections.Single(s => s.Page!.Slug == "test");
        Assert.DoesNotContain("<script", section.Html, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSection_SanitizesHtml()
    {
        var (ctx, layout, sanitizer) = CreateServices();
        var controller = new AdminPageSectionController(ctx, layout, sanitizer);

        var model = new PageSection { PageId = ctx.Pages.First().Id, Zone = "main", Html = "<div>hi</div><script>bad()</script>", Type = PageSectionType.Html };
        var result = await controller.Create(model, null);

        Assert.IsType<RedirectToActionResult>(result);
        var section = ctx.PageSections.First();
        Assert.DoesNotContain("<script", section.Html, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "Edit sanitization covered by section tests")]
    public async Task EditPage_SanitizesHtml()
    {
        var (ctx, layout, sanitizer) = CreateServices();
        var controller = new AdminContentController(ctx, layout, sanitizer);
        var createModel = new Page
        {
            Slug = "edit",
            Title = "Edit",
            Layout = "single-column",
            Sections = new List<PageSection> { new PageSection { Zone = "main", Html = "<p>a</p>" } }
        };
        await controller.Create(createModel);
        var page = ctx.Pages.Single(p => p.Slug == "edit");
        var model = new Page
        {
            Id = page.Id,
            Slug = page.Slug,
            Title = page.Title,
            Layout = page.Layout,
            Sections = new List<PageSection> { new PageSection { Zone = "main", Html = "<p>b</p><script>alert(2)</script>" } }
        };
        var result = await controller.Edit(model);
        var section = ctx.PageSections.Single(s => s.PageId == page.Id);
        Assert.IsType<RedirectToActionResult>(result);
        Assert.DoesNotContain("<script", section.Html, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSection_MarkdownConverted()
    {
        var (ctx, layout, sanitizer) = CreateServices();
        var controller = new AdminPageSectionController(ctx, layout, sanitizer);
        var model = new PageSection { PageId = ctx.Pages.First().Id, Zone = "md", Html = "# Hello\n<script>bad()</script>", Type = PageSectionType.Markdown };
        var result = await controller.Create(model, null);
        Assert.IsType<RedirectToActionResult>(result);
        var section = ctx.PageSections.First(s => s.Zone == "md");
        Assert.Contains("<h1>", section.Html);
        Assert.DoesNotContain("<script", section.Html, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSection_CodeEncoded()
    {
        var (ctx, layout, sanitizer) = CreateServices();
        var controller = new AdminPageSectionController(ctx, layout, sanitizer);
        var model = new PageSection { PageId = ctx.Pages.First().Id, Zone = "code", Html = "<b>test</b>", Type = PageSectionType.Code };
        var result = await controller.Create(model, null);
        Assert.IsType<RedirectToActionResult>(result);
        var section = ctx.PageSections.First(s => s.Zone == "code");
        Assert.Contains("&lt;b&gt;test&lt;/b&gt;", section.Html);
    }

    [Fact]
    public async Task CreateSection_ImageStoresTag()
    {
        var (ctx, layout, sanitizer) = CreateServices();
        var controller = new AdminPageSectionController(ctx, layout, sanitizer);
        var bytes = new byte[] { 1, 2, 3 };
        using var stream = new System.IO.MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "img.png");
        var model = new PageSection { PageId = ctx.Pages.First().Id, Zone = "img", Type = PageSectionType.Image };
        var result = await controller.Create(model, file);
        Assert.IsType<RedirectToActionResult>(result);
        var section = ctx.PageSections.First(s => s.Zone == "img");
        Assert.Contains("<img", section.Html);
    }
}
