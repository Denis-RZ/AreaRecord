using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;
using MyWebApp.Models;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyWebApp.Services;

namespace MyWebApp.Controllers;

public class SetupController : BaseController
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly SchemaValidator _validator;

    public SetupController(ApplicationDbContext context, IConfiguration config, ILogger<SetupController> logger, IWebHostEnvironment env, SchemaValidator validator)
        : base(context, logger)
    {
        _config = config;
        _env = env;
        _validator = validator;
    }

    public IActionResult Index()
    {
        var error = TempData != null ? TempData["DbError"]?.ToString() : null;
        var result = TempData != null ? TempData["SetupResult"]?.ToString() : null;
        var connection = _config.GetConnectionString("DefaultConnection") ?? string.Empty;
        var provider = _config["DatabaseProvider"] ?? "SqlServer";
        ConnectionHelper.ParseConnectionString(provider, connection, out var server, out var database, out var user, out var pass);
        var model = new SetupViewModel
        {
            CanConnect = CheckDatabase(),
            ConnectionString = connection,
            Provider = provider,
            Server = server,
            Database = database,
            Username = user,
            Password = pass,
            ErrorMessage = error,
            ResultMessage = result
        };
        var validation = _validator.Validate();
        model.SchemaValid = validation.Success;
        model.SchemaMessages = validation.Messages;
        return View(model);
    }

    [HttpPost]
    public IActionResult UseSqlite()
    {
        if (_config is IConfigurationRoot root)
        {
            foreach (var provider in root.Providers)
            {
                provider.Set("ConnectionStrings:DefaultConnection", "Data Source=mywebapp.db");
                provider.Set("DatabaseProvider", "Sqlite");
            }
        }
        Db.Database.EnsureCreated();
        TempData["SetupResult"] = "Switched to SQLite.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Test(string provider, string server, string database, string username, string password)
    {
        var connectionString = ConnectionHelper.BuildConnectionString(provider, server, database, username, password);
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        switch (provider.ToLowerInvariant())
        {
            case "npgsql":
            case "postgresql":
                optionsBuilder.UseNpgsql(connectionString);
                break;
            case "sqlite":
                optionsBuilder.UseSqlite(connectionString);
                break;
            default:
                optionsBuilder.UseSqlServer(connectionString);
                break;
        }

        try
        {
            using var testDb = new ApplicationDbContext(optionsBuilder.Options);
            var connected = testDb.Database.CanConnect();
            if (connected)
            {
                testDb.Database.EnsureCreated();
            }
            TempData["SetupResult"] = connected ? "Connection successful" : "Connection failed";
        }
        catch (Exception ex)
        {
            TempData["SetupResult"] = "Connection failed: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Save(string provider, string server, string database, string username, string password)
    {
        try
        {
            var connectionString = ConnectionHelper.BuildConnectionString(provider, server, database, username, password);
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            switch (provider.ToLowerInvariant())
            {
                case "npgsql":
                case "postgresql":
                    optionsBuilder.UseNpgsql(connectionString);
                    break;
                case "sqlite":
                    optionsBuilder.UseSqlite(connectionString);
                    break;
                default:
                    optionsBuilder.UseSqlServer(connectionString);
                    break;
            }
            var path = System.IO.Path.Combine(_env.ContentRootPath, "appsettings.json");
            var json = System.IO.File.ReadAllText(path);
            var obj = JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
            if (obj["ConnectionStrings"] is not JsonObject cs)
            {
                cs = new JsonObject();
                obj["ConnectionStrings"] = cs;
            }
            cs["DefaultConnection"] = connectionString;
            obj["DatabaseProvider"] = provider;
            System.IO.File.WriteAllText(path, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            if (_config is IConfigurationRoot root)
            {
                foreach (var p in root.Providers)
                {
                    p.Set("ConnectionStrings:DefaultConnection", connectionString);
                    p.Set("DatabaseProvider", provider);
                }
            }

            // ensure schema exists using the new settings
            using var context = new ApplicationDbContext(optionsBuilder.Options);
            context.Database.EnsureCreated();
            TempData["SetupResult"] = "Configuration saved.";
        }
        catch (Exception ex)
        {
            TempData["SetupResult"] = "Save failed: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Seed()
    {
        if (!CheckDatabase())
        {
            return RedirectToSetup();
        }

        try
        {
            if (!Db.Recordings.Any())
            {
                Db.Recordings.AddRange(
                    new Recording { Name = "Sample1", Created = DateTime.UtcNow },
                    new Recording { Name = "Sample2", Created = DateTime.UtcNow }
                );
            }
            if (!Db.Downloads.Any())
            {
                Db.Downloads.AddRange(
                    new Download { UserIP = "127.0.0.1", UserAgent = "seed", DownloadTime = DateTime.UtcNow, IsSuccessful = true },
                    new Download { UserIP = "127.0.0.2", UserAgent = "seed", DownloadTime = DateTime.UtcNow, IsSuccessful = false }
                );
            }
            Db.SaveChanges();
            TempData["SetupResult"] = "Sample data inserted.";
        }
        catch (Exception ex)
        {
            TempData["SetupResult"] = "Seeding failed: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Import() => View();
}
