using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using MyWebApp.Services;
using MyWebApp.Data;
using MyWebApp.Models;


namespace MyWebApp.Controllers;

public class DownloadController : BaseController
{
    private readonly ILogger<DownloadController> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly CacheService _cache;
    private readonly CaptchaService _captchaService;

    public DownloadController(ApplicationDbContext context,
        ILogger<DownloadController> logger,
        IMemoryCache memoryCache,
        CacheService cache,
        CaptchaService captchaService)
        : base(context, logger)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        _cache = cache;
        _captchaService = captchaService;
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
        _captchaService.CreateChallenge();
        ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
        var files = await Db.DownloadFiles.AsNoTracking().ToListAsync();
        return View(files);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string captcha, int fileId)
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
            _captchaService.CreateChallenge();
            ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
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
            catch (DbUpdateException ex)
            {
                return RedirectToSetup(ex);
            }
            ModelState.AddModelError(string.Empty, "Invalid request.");
            ViewBag.TotalDownloads = await _cache.GetOrCreateAsync(CacheKeys.TotalDownloads, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await Db.Downloads.AsNoTracking().CountAsync(d => d.IsSuccessful);
            });
            _captchaService.CreateChallenge();
            ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
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
            catch (DbUpdateException ex)
            {
                return RedirectToSetup(ex);
            }
            ViewBag.TotalDownloads = await _cache.GetOrCreateAsync(CacheKeys.TotalDownloads, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await Db.Downloads.AsNoTracking().CountAsync(d => d.IsSuccessful);
            });
            _captchaService.CreateChallenge();
            ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
            var filesRate = await Db.DownloadFiles.AsNoTracking().ToListAsync();
            return View(filesRate);
        }

        if (!_captchaService.Validate(captcha))
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
            catch (DbUpdateException ex)
            {
                return RedirectToSetup(ex);
            }
            ViewBag.TotalDownloads = await _cache.GetOrCreateAsync(CacheKeys.TotalDownloads, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await Db.Downloads.AsNoTracking().CountAsync(d => d.IsSuccessful);
            });
            _captchaService.CreateChallenge();
            ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
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
        catch (DbUpdateException ex)
        {
            return RedirectToSetup(ex);
        }
        SetRateLimit(ip);
        if (file.Data != null)
        {
            return File(file.Data, file.ContentType ?? "application/octet-stream", file.FileName);
        }
        return Redirect("/files/" + file.FileName);
    }

    [HttpGet("File/{id}")]
    public async Task<IActionResult> GetFile(int id)
    {
        var file = await Db.DownloadFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        if (file == null || file.Data == null)
        {
            return NotFound();
        }
        return File(file.Data, file.ContentType ?? "application/octet-stream", file.FileName);
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

}
