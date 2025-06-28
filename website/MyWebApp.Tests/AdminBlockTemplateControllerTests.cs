using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using MyWebApp.Controllers;
using MyWebApp.Data;
using MyWebApp.Models;
using MyWebApp.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

public class AdminBlockTemplateControllerTests
{
    private static (AdminBlockTemplateController controller, ApplicationDbContext ctx, SqliteConnection conn) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(conn)
            .Options;
        var ctx = new ApplicationDbContext(options);
        ctx.Database.EnsureCreated();
        var sanitizer = new HtmlSanitizerService();
        var controller = new AdminBlockTemplateController(ctx, sanitizer);
        return (controller, ctx, conn);
    }

    [Fact]
    public async Task AddToPage_InvalidReturnsViewWithSelections()
    {
        var tuple = Create();
        using var connection = tuple.conn;
        var ctx = tuple.ctx;
        var controller = tuple.controller;
        var template = new BlockTemplate { Name = "b", Html = "x" };
        ctx.BlockTemplates.Add(template);
        ctx.SaveChanges();

        var homeId = ctx.Pages.Single(p => p.Slug == "home").Id;
        var result = await controller.AddToPage(template.Id, new List<int> { homeId }, "", "Admin");
        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        var selected = Assert.IsType<List<int>>(controller.ViewBag.SelectedPageIds);
        Assert.Contains(homeId, selected);
        Assert.Equal("", controller.ViewBag.SelectedZone as string);
        Assert.Equal("Admin", controller.ViewBag.SelectedRole as string);
    }

    [Fact]
    public async Task Create_InvalidModelPreservesSelections()
    {
        var tuple = Create();
        using var connection = tuple.conn;
        var ctx = tuple.ctx;
        var controller = tuple.controller;
        var homeId = ctx.Pages.Single(p => p.Slug == "home").Id;
        var model = new BlockTemplate();
        controller.ModelState.AddModelError("Name", "required");
        var result = await controller.Create(model, new List<int> { homeId }, "main", "Admin");
        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        var selected = Assert.IsType<List<int>>(controller.ViewBag.SelectedPageIds);
        Assert.Contains(homeId, selected);
        Assert.Equal("main", controller.ViewBag.SelectedZone as string);
        Assert.Equal("Admin", controller.ViewBag.SelectedRole as string);
    }
}
