using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyWebApp.Data;
using Microsoft.AspNetCore.Http;
using System;

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
            Db.Database.EnsureCreated();
            return Db.Database.CanConnect();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Database connectivity check failed");
            return false;
        }
    }

    protected bool HasRole(string role)
    {
        var roles = HttpContext.Session.GetString("Roles")?.Split(',') ?? Array.Empty<string>();
        return roles.Contains(role);
    }

    protected bool IsAdmin()
    {
        return HasRole("Admin");
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

        if (IsAdmin())
        {
            return RedirectToAction("Index", "Setup");
        }

        var returnUrl = Url.Action("Index", "Setup");
        return RedirectToAction("Login", "Account", new { returnUrl });
    }
}
