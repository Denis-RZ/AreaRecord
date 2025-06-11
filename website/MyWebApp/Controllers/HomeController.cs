using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyWebApp.Models;

namespace MyWebApp.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly MyWebApp.Data.ApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, MyWebApp.Data.ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public IActionResult Index()
    {
        _context.Recordings.Add(new Recording { Name = "Visit", Created = DateTime.UtcNow });
        try
        {
            _context.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save recording");
        }
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

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
