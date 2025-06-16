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
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Abstractions;

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
        var http = new DefaultHttpContext { Session = new DummySession() };
        var controller = new HomeController(NullLogger<HomeController>.Instance, context, cache)
        {
            TempData = new TempDataDictionary(http, new FakeTempProvider())
        };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.Url = new DummyUrlHelper();
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

    private class DummyUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();
        public string? Action(UrlActionContext actionContext) => "/";
        public string Content(string contentPath) => contentPath;
        public bool IsLocalUrl(string url) => true;
        public string? Link(string routeName, object? values) => null;
        public string? RouteUrl(UrlRouteContext routeContext) => "/";
    }

    private class DummySession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString();
        public IEnumerable<string> Keys => _store.Keys;
        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value);
    }
}
