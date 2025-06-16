using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Controllers;
using MyWebApp.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using MyWebApp.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Xunit;
using MyWebApp.Services;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

class FakeEnv : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "Test";
    public string WebRootPath { get; set; } = "/tmp";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = "/tmp";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

public class SetupControllerTests
{
    [Fact]
    public void Index_ReturnsView_WithModel()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        var config = new ConfigurationBuilder().Build();
        var env = new FakeEnv();
        var validator = new SchemaValidator(context);
        var controller = new SetupController(context, config, NullLogger<SetupController>.Instance, env, validator);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Session = new DummySession() } };
        controller.HttpContext.Session.SetString("IsAdmin", "true");

        var result = controller.Index();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
    }

    [Fact]
    public void Index_HandlesConnectionFailure()
    {
        var options = new DbContextOptions<ApplicationDbContext>();
        using var context = new ApplicationDbContext(options);
        var config = new ConfigurationBuilder().Build();
        var env = new FakeEnv();
        var validator = new SchemaValidator(context);
        var controller = new SetupController(context, config, NullLogger<SetupController>.Instance, env, validator);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Session = new DummySession() } };
        controller.HttpContext.Session.SetString("IsAdmin", "true");

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SetupViewModel>(viewResult.Model);
        Assert.False(model.CanConnect);
    }

    [Fact]
    public void Seed_InsertsSampleData()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        var config = new ConfigurationBuilder().Build();
        var env = new FakeEnv();
        var validator = new SchemaValidator(context);
        var httpContext = new DefaultHttpContext { Session = new DummySession() };
        var controller = new SetupController(context, config, NullLogger<SetupController>.Instance, env, validator)
        {
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(httpContext, new FakeTempProvider()),
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
        controller.HttpContext.Session.SetString("IsAdmin", "true");
        var result = controller.Seed();
        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotEmpty(context.Recordings);
        Assert.NotEmpty(context.Downloads);
    }

    private class FakeTempProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    private class DummySession : ISession
    {
        private Dictionary<string, byte[]> _store = new();
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
