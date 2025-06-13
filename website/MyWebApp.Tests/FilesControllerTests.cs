using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyWebApp.Controllers;
using MyWebApp.Data;
using MyWebApp.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

public class FilesControllerTests
{
    private static ApplicationDbContext CreateContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var ctx = new ApplicationDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task Index_ReturnsViewWithStats()
    {
        using var ctx = CreateContext(out var conn);
        var file = new DownloadFile { FileName = "test.txt", Description = "d", Created = DateTime.UtcNow };
        ctx.DownloadFiles.Add(file);
        ctx.Downloads.Add(new Download { DownloadFile = file, DownloadTime = DateTime.UtcNow, IsSuccessful = true, UserIP = "1", UserAgent = "a" });
        ctx.SaveChanges();
        var controller = new FilesController(ctx, NullLogger<FilesController>.Instance);
        var result = await controller.Index();
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<System.Collections.Generic.List<FileStatsViewModel>>(view.Model);
        Assert.Single(model);
        Assert.Equal(1, model[0].DownloadCount);
    }

    [Fact]
    public async Task Create_PostAddsFile()
    {
        using var ctx = CreateContext(out var conn);
        var controller = new FilesController(ctx, NullLogger<FilesController>.Instance);
        var file = new DownloadFile { FileName = "new.bin", Description = "x" };
        var result = await controller.Create(file);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(FilesController.Index), redirect.ActionName);
        Assert.Equal(1, ctx.DownloadFiles.Count());
    }
}
