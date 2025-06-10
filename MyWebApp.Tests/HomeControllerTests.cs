using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MyWebApp.Controllers;
using Xunit;

public class HomeControllerTests
{
    [Fact]
    public void Index_ReturnsView()
    {
        var controller = new HomeController(NullLogger<HomeController>.Instance);
        var result = controller.Index();
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Privacy_ReturnsView()
    {
        var controller = new HomeController(NullLogger<HomeController>.Instance);
        var result = controller.Privacy();
        Assert.IsType<ViewResult>(result);
    }
}
