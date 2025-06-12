using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyWebApp.Models;

namespace MyWebApp.Controllers;

public class HomeController : BaseController
{
    public HomeController(ILogger<HomeController> logger, MyWebApp.Data.ApplicationDbContext context)
        : base(context, logger)
    {
    }

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
