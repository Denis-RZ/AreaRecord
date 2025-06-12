using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MyWebApp.Data;
using MyWebApp.Filters;
using MyWebApp.Models;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyWebApp.Controllers
{
    [BasicAuth]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminController> _logger;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private static readonly DateTime _startTime = DateTime.UtcNow;

        public AdminController(ApplicationDbContext context, ILogger<AdminController> logger, IConfiguration config, IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _config = config;
            _env = env;
        }

        private bool CheckDatabase()
        {
            try
            {
                return _context.Database.CanConnect();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connectivity check failed");
                return false;
            }
        }

        public IActionResult Index()
        {
            var model = new AdminDashboardViewModel
            {
                TotalDownloads = _context.Downloads.Count(d => d.IsSuccessful),
                FailedDownloads = _context.Downloads.Count(d => !d.IsSuccessful),
                DownloadsLast24h = _context.Downloads.Count(d => d.DownloadTime > DateTime.UtcNow.AddDays(-1) && d.IsSuccessful),
                TopCountries = _context.Downloads.Where(d => d.IsSuccessful && d.Country != null)
                    .GroupBy(d => d.Country)
                    .Select(g => new CountryCount { Country = g.Key!, Count = g.Count() })
                    .OrderByDescending(c => c.Count)
                    .Take(5)
                    .ToList(),
                SystemInfo = new SystemInfoViewModel
                {
                    Uptime = DateTime.UtcNow - _startTime,
                    DotNetVersion = Environment.Version.ToString(),
                    Started = _startTime
                }
            };

            return View(model);
        }

        public IActionResult Downloads(int page = 1, string? search = null, string? status = null)
        {
            const int PageSize = 50;
            var query = _context.Downloads.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(d => d.UserIP.Contains(search));
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status.Equals("successful", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(d => d.IsSuccessful);
                }
                else if (status.Equals("failed", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(d => !d.IsSuccessful);
                }
            }

            var total = query.Count();
            var downloads = query.OrderByDescending(d => d.DownloadTime)
                                  .Skip((page - 1) * PageSize)
                                  .Take(PageSize)
                                  .ToList();
            var model = new DownloadStatsViewModel
            {
                Downloads = downloads,
                Page = page,
                TotalPages = (int)Math.Ceiling(total / (double)PageSize),
                Search = search,
                Status = status
            };
            return View(model);
        }

        public IActionResult Stats()
        {
            var startDate = DateTime.UtcNow.AddDays(-30).Date;
            var daily = _context.Downloads.Where(d => d.IsSuccessful && d.DownloadTime >= startDate)
                .AsEnumerable()
                .GroupBy(d => d.DownloadTime.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(g => g.Date)
                .ToList();

            var country = _context.Downloads.Where(d => d.IsSuccessful && d.Country != null)
                .GroupBy(d => d.Country)
                .Select(g => new { Country = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToList();

            var agents = _context.Downloads.Where(d => d.IsSuccessful)
                .GroupBy(d => d.UserAgent)
                .Select(g => new { Agent = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(10)
                .ToList();

            ViewBag.DailyData = daily;
            ViewBag.CountryData = country;
            ViewBag.AgentData = agents;
            return View();
        }

        public IActionResult DbSettings()
        {
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
                Password = pass
            };
            return View(model);
        }

        [HttpPost]
        public IActionResult TestDb(string provider, string server, string database, string username, string password)
        {
            var conn = ConnectionHelper.BuildConnectionString(provider, server, database, username, password);
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            switch (provider.ToLowerInvariant())
            {
                case "npgsql":
                case "postgresql":
                    optionsBuilder.UseNpgsql(conn);
                    break;
                case "sqlite":
                    optionsBuilder.UseSqlite(conn);
                    break;
                default:
                    optionsBuilder.UseSqlServer(conn);
                    break;
            }

            try
            {
                using var testDb = new ApplicationDbContext(optionsBuilder.Options);
                TempData["SetupResult"] = testDb.Database.CanConnect() ? "Connection successful" : "Connection failed";
            }
            catch (Exception ex)
            {
                TempData["SetupResult"] = "Connection failed: " + ex.Message;
            }
            return RedirectToAction(nameof(DbSettings));
        }

        [HttpPost]
        public IActionResult SaveDb(string provider, string server, string database, string username, string password)
        {
            try
            {
                var conn = ConnectionHelper.BuildConnectionString(provider, server, database, username, password);
                var path = System.IO.Path.Combine(_env.ContentRootPath, "appsettings.json");
                var json = System.IO.File.ReadAllText(path);
                var obj = JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
                if (obj["ConnectionStrings"] is not JsonObject cs)
                {
                    cs = new JsonObject();
                    obj["ConnectionStrings"] = cs;
                }
                cs["DefaultConnection"] = conn;
                obj["DatabaseProvider"] = provider;
                System.IO.File.WriteAllText(path, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

                if (_config is IConfigurationRoot root)
                {
                    foreach (var p in root.Providers)
                    {
                        p.Set("ConnectionStrings:DefaultConnection", conn);
                        p.Set("DatabaseProvider", provider);
                    }
                }

                TempData["SetupResult"] = "Configuration saved.";
            }
            catch (Exception ex)
            {
                TempData["SetupResult"] = "Save failed: " + ex.Message;
            }
            return RedirectToAction(nameof(DbSettings));
        }

        public IActionResult Logs()
        {
            // For demo purposes, logs are not implemented
            return View();
        }
    }
}
