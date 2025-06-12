using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Controllers;
using MyWebApp.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using MyWebApp.Models;
using Xunit;

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
        var controller = new SetupController(context, config, NullLogger<SetupController>.Instance);

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
        var controller = new SetupController(context, config, NullLogger<SetupController>.Instance);

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SetupViewModel>(viewResult.Model);
        Assert.False(model.CanConnect);
    }
}
