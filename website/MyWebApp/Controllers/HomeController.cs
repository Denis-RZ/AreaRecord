using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyWebApp.Models;
using MyWebApp.Services;

namespace MyWebApp.Controllers;

public class HomeController : BaseController
{
    private readonly CacheService _cache;

    public HomeController(ILogger<HomeController> logger, MyWebApp.Data.ApplicationDbContext context, CacheService cache)
        : base(context, logger)
    {
        _cache = cache;
    }

    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public IActionResult Index()
    {
        if (!CheckDatabase())
        {
            return RedirectToSetup();
        }

        Db.Recordings.Add(new Recording { Name = "Visit", Created = DateTime.UtcNow });
        try
        {
            Db.SaveChanges();
        }
        catch (Exception ex)
        {
            return RedirectToSetup(ex);
        }
        return View();
    }

    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public IActionResult Faq()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
