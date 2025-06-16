using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MyWebApp.Controllers;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;
using Microsoft.Extensions.Caching.Memory;
using MyWebApp.Services;
using Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

public class HomeControllerTests
{
    [Fact]
    public void Index_ReturnsView()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<MyWebApp.Data.ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = new MyWebApp.Data.ApplicationDbContext(options);
        context.Database.EnsureCreated();
        var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new CacheService(memory);
        var controller = new HomeController(NullLogger<HomeController>.Instance, context, cache);
        var result = controller.Index();
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Privacy_ReturnsView()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<MyWebApp.Data.ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = new MyWebApp.Data.ApplicationDbContext(options);
        context.Database.EnsureCreated();
        var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new CacheService(memory);
        var controller = new HomeController(NullLogger<HomeController>.Instance, context, cache);
        var result = controller.Privacy();
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Faq_ReturnsView()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<MyWebApp.Data.ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = new MyWebApp.Data.ApplicationDbContext(options);
        context.Database.EnsureCreated();
        var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new CacheService(memory);
        var controller = new HomeController(NullLogger<HomeController>.Instance, context, cache);
        var result = controller.Faq();
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Index_RedirectsWhenDbFails()
    {
        var options = new DbContextOptions<ApplicationDbContext>();
        using var context = new ApplicationDbContext(options);
        var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new CacheService(memory);
        var controller = new HomeController(NullLogger<HomeController>.Instance, context, cache)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempProvider())
        };
        var result = controller.Index();
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
        Assert.Equal("Account", redirect.ControllerName);
    }

    private class FakeTempProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
