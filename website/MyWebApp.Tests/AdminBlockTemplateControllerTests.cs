using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using MyWebApp.Controllers;
using MyWebApp.Data;
using MyWebApp.Models;
using MyWebApp.Services;
using System.Collections.Generic;
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
        ctx.BlockTemplates.Add(new BlockTemplate { Id = 1, Name = "b", Html = "x" });
        ctx.Pages.Add(new Page { Id = 1, Slug = "home", Title = "Home", Layout = "single-column" });
        ctx.Roles.Add(new Role { Id = 1, Name = "Admin" });
        ctx.SaveChanges();

        var result = await controller.AddToPage(1, new List<int> { 1 }, "", "Admin");
        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        var selected = Assert.IsType<List<int>>(controller.ViewBag.SelectedPageIds);
        Assert.Contains(1, selected);
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
        ctx.Pages.Add(new Page { Id = 1, Slug = "home", Title = "Home", Layout = "single-column" });
        ctx.Roles.Add(new Role { Id = 1, Name = "Admin" });
        ctx.SaveChanges();

        var model = new BlockTemplate();
        controller.ModelState.AddModelError("Name", "required");
        var result = await controller.Create(model, new List<int> { 1 }, "main", "Admin");
        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        var selected = Assert.IsType<List<int>>(controller.ViewBag.SelectedPageIds);
        Assert.Contains(1, selected);
        Assert.Equal("main", controller.ViewBag.SelectedZone as string);
        Assert.Equal("Admin", controller.ViewBag.SelectedRole as string);
    }
}
