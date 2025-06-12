using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Services;
using MyWebApp.Data;
using MyWebApp.Models;

namespace MyWebApp.Controllers;

public class DownloadController : BaseController
{
    private readonly ILogger<DownloadController> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly CacheService _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public DownloadController(ApplicationDbContext context,
        ILogger<DownloadController> logger,
        IMemoryCache memoryCache,
        CacheService cache,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
        : base(context, logger)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!CheckDatabase())
        {
            return RedirectToSetup();
        }
        ViewBag.TotalDownloads = await _cache.GetOrCreateAsync(CacheKeys.TotalDownloads, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await Db.Downloads.AsNoTracking().CountAsync(d => d.IsSuccessful);
        });
        ViewBag.SiteKey = _configuration["Captcha:SiteKey"] ?? string.Empty;
        var files = await Db.DownloadFiles.AsNoTracking().ToListAsync();
        return View(files);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string token, int fileId)
    {
        if (!CheckDatabase())
        {
            return RedirectToSetup();
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers["User-Agent"].ToString();
        var sessionId = HttpContext.Session.Id;

        var file = await Db.DownloadFiles.FindAsync(fileId);
        if (file == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid file.");
            ViewBag.TotalDownloads = await _cache.GetOrCreateAsync(CacheKeys.TotalDownloads, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await Db.Downloads.AsNoTracking().CountAsync(d => d.IsSuccessful);
            });
            ViewBag.SiteKey = _configuration["Captcha:SiteKey"] ?? string.Empty;
            var files = await Db.DownloadFiles.AsNoTracking().ToListAsync();
            return View(files);
        }

        var download = new Download
        {
            UserIP = ip,
            UserAgent = userAgent,
            DownloadTime = DateTime.UtcNow,
            SessionId = sessionId,
            IsSuccessful = false,
            DownloadFileId = file.Id
        };

        if (!ValidateUserAgent(userAgent))
        {
            _logger.LogWarning("Invalid user agent {Agent}", userAgent);
            Db.Downloads.Add(download);
            try
            {
                await Db.SaveChangesAsync();
                _cache.Remove(CacheKeys.TotalDownloads);
                _cache.Remove(CacheKeys.AdminDashboard);
                _cache.Remove(CacheKeys.DownloadStats);
                _cache.Remove(CacheKeys.TopCountries);
                _cache.Remove(CacheKeys.AgentStats);
            }
            catch (Exception ex)
            {
                return RedirectToSetup(ex);
            }
            ModelState.AddModelError(string.Empty, "Invalid request.");
            ViewBag.TotalDownloads = await _cache.GetOrCreateAsync(CacheKeys.TotalDownloads, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await Db.Downloads.AsNoTracking().CountAsync(d => d.IsSuccessful);
            });
            ViewBag.SiteKey = _configuration["Captcha:SiteKey"] ?? string.Empty;
            var filesFail = await Db.DownloadFiles.AsNoTracking().ToListAsync();
            return View(filesFail);
        }

        if (IsRateLimited(ip))
        {
            _logger.LogInformation("Rate limit hit for {IP}", ip);
            ModelState.AddModelError(string.Empty, "Please wait a few minutes before trying again.");
            Db.Downloads.Add(download);
            try
            {
                await Db.SaveChangesAsync();
                _cache.Remove(CacheKeys.TotalDownloads);
                _cache.Remove(CacheKeys.AdminDashboard);
                _cache.Remove(CacheKeys.DownloadStats);
                _cache.Remove(CacheKeys.TopCountries);
                _cache.Remove(CacheKeys.AgentStats);
            }
            catch (Exception ex)
            {
                return RedirectToSetup(ex);
            }
            ViewBag.TotalDownloads = await _cache.GetOrCreateAsync(CacheKeys.TotalDownloads, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await Db.Downloads.AsNoTracking().CountAsync(d => d.IsSuccessful);
            });
            ViewBag.SiteKey = _configuration["Captcha:SiteKey"] ?? string.Empty;
            var filesRate = await Db.DownloadFiles.AsNoTracking().ToListAsync();
            return View(filesRate);
        }

        if (!await VerifyCaptchaAsync(token, ip))
        {
            _logger.LogInformation("Captcha failed for {IP}", ip);
            ModelState.AddModelError(string.Empty, "CAPTCHA validation failed.");
            Db.Downloads.Add(download);
            try
            {
                await Db.SaveChangesAsync();
                _cache.Remove(CacheKeys.TotalDownloads);
                _cache.Remove(CacheKeys.AdminDashboard);
                _cache.Remove(CacheKeys.DownloadStats);
                _cache.Remove(CacheKeys.TopCountries);
                _cache.Remove(CacheKeys.AgentStats);
            }
            catch (Exception ex)
            {
                return RedirectToSetup(ex);
            }
            ViewBag.TotalDownloads = await _cache.GetOrCreateAsync(CacheKeys.TotalDownloads, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await Db.Downloads.AsNoTracking().CountAsync(d => d.IsSuccessful);
            });
            ViewBag.SiteKey = _configuration["Captcha:SiteKey"] ?? string.Empty;
            var filesCaptcha = await Db.DownloadFiles.AsNoTracking().ToListAsync();
            return View(filesCaptcha);
        }

        download.IsSuccessful = true;
        Db.Downloads.Add(download);
        try
        {
            await Db.SaveChangesAsync();
            _cache.Remove(CacheKeys.TotalDownloads);
            _cache.Remove(CacheKeys.AdminDashboard);
            _cache.Remove(CacheKeys.DownloadStats);
            _cache.Remove(CacheKeys.TopCountries);
            _cache.Remove(CacheKeys.AgentStats);
        }
        catch (Exception ex)
        {
            return RedirectToSetup(ex);
        }
        SetRateLimit(ip);
        return Redirect("/files/" + file.FileName);
    }

    private bool ValidateUserAgent(string userAgent) => !string.IsNullOrWhiteSpace(userAgent);

    private bool IsRateLimited(string ip)
    {
        if (_memoryCache.TryGetValue(ip, out _))
        {
            return true;
        }
        return false;
    }

    private void SetRateLimit(string ip)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };
        _memoryCache.Set(ip, true, options);
    }

    private async Task<bool> VerifyCaptchaAsync(string token, string ip)
    {
        var secret = _configuration["Captcha:SecretKey"];
        var verifyUrl = _configuration["Captcha:VerifyUrl"];
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(verifyUrl))
        {
            _logger.LogWarning("Captcha secret or verify url not configured");
            return false;
        }

        var client = _httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "secret", secret },
            { "response", token },
            { "remoteip", ip }
        });

        try
        {
            var response = await client.PostAsync(verifyUrl, content);
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Captcha verification failed");
        }
        return false;
    }
}
