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

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<SetupViewModel>(viewResult.Model);
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
        var controller = new SetupController(context, config, NullLogger<SetupController>.Instance, env, validator)
        {
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(new DefaultHttpContext(), new FakeTempProvider())
        };
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
}
