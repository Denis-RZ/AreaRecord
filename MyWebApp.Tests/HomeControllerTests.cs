using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MyWebApp.Controllers;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;
using Xunit;

public class HomeControllerTests
{
    [Fact]
    public void Index_ReturnsView()
    {
        var options = new DbContextOptionsBuilder<MyWebApp.Data.ApplicationDbContext>()
            .UseInMemoryDatabase("IndexDb")
            .Options;
        using var context = new MyWebApp.Data.ApplicationDbContext(options);
        var controller = new HomeController(NullLogger<HomeController>.Instance, context);
        var result = controller.Index();
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Privacy_ReturnsView()
    {
        var options = new DbContextOptionsBuilder<MyWebApp.Data.ApplicationDbContext>()
            .UseInMemoryDatabase("PrivacyDb")
            .Options;
        using var context = new MyWebApp.Data.ApplicationDbContext(options);
        var controller = new HomeController(NullLogger<HomeController>.Instance, context);
        var result = controller.Privacy();
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Faq_ReturnsView()
    {
        var options = new DbContextOptionsBuilder<MyWebApp.Data.ApplicationDbContext>()
            .UseInMemoryDatabase("FaqDb")
            .Options;
        using var context = new MyWebApp.Data.ApplicationDbContext(options);
        var controller = new HomeController(NullLogger<HomeController>.Instance, context);
        var result = controller.Faq();
        Assert.IsType<ViewResult>(result);
    }
}
