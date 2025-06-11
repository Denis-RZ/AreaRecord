using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MyWebApp.Data;
using MyWebApp.Models;

namespace MyWebApp.Controllers;

public class DownloadController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DownloadController> _logger;
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public DownloadController(ApplicationDbContext context,
        ILogger<DownloadController> logger,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.TotalDownloads = _context.Downloads.Count(d => d.IsSuccessful);
        ViewBag.SiteKey = _configuration["Captcha:SiteKey"] ?? string.Empty;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string token)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers["User-Agent"].ToString();
        var sessionId = HttpContext.Session.Id;

        var download = new Download
        {
            UserIP = ip,
            UserAgent = userAgent,
            DownloadTime = DateTime.UtcNow,
            SessionId = sessionId,
            IsSuccessful = false
        };

        if (!ValidateUserAgent(userAgent))
        {
            _logger.LogWarning("Invalid user agent {Agent}", userAgent);
            _context.Downloads.Add(download);
            await _context.SaveChangesAsync();
            ModelState.AddModelError(string.Empty, "Invalid request.");
            ViewBag.TotalDownloads = _context.Downloads.Count(d => d.IsSuccessful);
            ViewBag.SiteKey = _configuration["Captcha:SiteKey"] ?? string.Empty;
            return View();
        }

        if (IsRateLimited(ip))
        {
            _logger.LogInformation("Rate limit hit for {IP}", ip);
            ModelState.AddModelError(string.Empty, "Please wait a few minutes before trying again.");
            _context.Downloads.Add(download);
            await _context.SaveChangesAsync();
            ViewBag.TotalDownloads = _context.Downloads.Count(d => d.IsSuccessful);
            ViewBag.SiteKey = _configuration["Captcha:SiteKey"] ?? string.Empty;
            return View();
        }

        if (!await VerifyCaptchaAsync(token, ip))
        {
            _logger.LogInformation("Captcha failed for {IP}", ip);
            ModelState.AddModelError(string.Empty, "CAPTCHA validation failed.");
            _context.Downloads.Add(download);
            await _context.SaveChangesAsync();
            ViewBag.TotalDownloads = _context.Downloads.Count(d => d.IsSuccessful);
            ViewBag.SiteKey = _configuration["Captcha:SiteKey"] ?? string.Empty;
            return View();
        }

        download.IsSuccessful = true;
        _context.Downloads.Add(download);
        await _context.SaveChangesAsync();
        SetRateLimit(ip);
        return Redirect("https://chrome.google.com/webstore/detail/screen-area-recorder-pro");
    }

    private bool ValidateUserAgent(string userAgent) => !string.IsNullOrWhiteSpace(userAgent);

    private bool IsRateLimited(string ip)
    {
        if (_cache.TryGetValue(ip, out _))
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
        _cache.Set(ip, true, options);
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
