using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyWebApp.Data;
using MyWebApp.Filters;
using MyWebApp.Models;

namespace MyWebApp.Controllers
{
    [BasicAuth]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminController> _logger;
        private static readonly DateTime _startTime = DateTime.UtcNow;

        public AdminController(ApplicationDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
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

        public IActionResult Logs()
        {
            // For demo purposes, logs are not implemented
            return View();
        }
    }
}
