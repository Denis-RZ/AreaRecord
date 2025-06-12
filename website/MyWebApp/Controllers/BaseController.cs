using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyWebApp.Data;

namespace MyWebApp.Controllers;

public abstract class BaseController : Controller
{
    protected readonly ApplicationDbContext Db;
    protected readonly ILogger Logger;

    protected BaseController(ApplicationDbContext db, ILogger logger)
    {
        Db = db;
        Logger = logger;
    }

    protected bool CheckDatabase()
    {
        try
        {
            return Db.Database.CanConnect();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Database connectivity check failed");
            return false;
        }
    }

    protected IActionResult RedirectToSetup(Exception? ex = null)
    {
        if (ex != null)
        {
            Logger.LogError(ex, "Database operation failed");
            TempData["DbError"] = ex.Message;
        }
        else
        {
            TempData["DbError"] = "Database connection failed";
        }
        return RedirectToAction("Index", "Setup");
    }
}
