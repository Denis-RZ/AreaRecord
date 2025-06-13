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
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("SetupSuccessDb")
            .Options;
        using var context = new ApplicationDbContext(options);
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
}
