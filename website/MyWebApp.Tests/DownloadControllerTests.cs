using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using MyWebApp.Controllers;
using MyWebApp.Data;
using MyWebApp.Models;
using MyWebApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

public class DownloadControllerTests
{

    private static (DownloadController controller, ApplicationDbContext ctx) Create(out SqliteConnection conn)
    {
        conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(conn)
            .Options;
        var ctx = new ApplicationDbContext(dbOptions);
        ctx.Database.EnsureCreated();
        var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new CacheService(memory);
        var accessor = new TestAccessor();
        var captchaService = new CaptchaService(accessor);
        var controller = new DownloadController(ctx, NullLogger<DownloadController>.Instance, memory, cache, captchaService);
        accessor.HttpContext = new DefaultHttpContext { Session = new DummySession() };
        controller.ControllerContext = new ControllerContext { HttpContext = accessor.HttpContext };
        return (controller, ctx);
    }

    private class TestAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
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

    [Fact]
    public async Task GetIndex_ReturnsFiles()
    {
        var tuple = Create(out var conn);
        using var connection = conn;
        var controller = tuple.controller;
        var ctx = tuple.ctx;
        ctx.DownloadFiles.Add(new DownloadFile { FileName = "f", Description = "d", Created = DateTime.UtcNow });
        ctx.SaveChanges();
        var result = await controller.Index();
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<DownloadFile>>(view.Model);
        Assert.Single(model);
    }

    [Fact]
    public async Task PostIndex_InvalidFile_ReturnsError()
    {
        var tuple = Create(out var conn);
        using var connection = conn;
        var controller = tuple.controller;
        var ctx = tuple.ctx;
        ctx.DownloadFiles.Add(new DownloadFile { Id = 1, FileName = "f", Description = "d", Created = DateTime.UtcNow });
        ctx.SaveChanges();
        var result = await controller.Index("code", 999);
        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        var list = Assert.IsAssignableFrom<IEnumerable<DownloadFile>>(view.Model);
        Assert.Contains(list, f => f.FileName == "f");
    }
}
