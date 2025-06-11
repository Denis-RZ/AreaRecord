using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Controllers;
using MyWebApp.Data;
using Xunit;

public class SetupControllerTests
{
    [Fact]
    public void Index_ReturnsView_WithBooleanModel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("SetupSuccessDb")
            .Options;
        using var context = new ApplicationDbContext(options);
        var controller = new SetupController(context);

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<bool>(viewResult.Model);
    }

    [Fact]
    public void Index_HandlesConnectionFailure()
    {
        var options = new DbContextOptions<ApplicationDbContext>();
        using var context = new ApplicationDbContext(options);
        var controller = new SetupController(context);

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False((bool)viewResult.Model);
    }
}
