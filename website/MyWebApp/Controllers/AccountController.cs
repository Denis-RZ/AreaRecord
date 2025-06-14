using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;
using MyWebApp.Models;
using System.Data.Common;

namespace MyWebApp.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _db;

    public AccountController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.ErrorMessage = "Invalid data";
            return View(model);
        }

        AdminCredential? cred = null;
        bool dbAvailable = true;
        try
        {
            dbAvailable = await _db.Database.CanConnectAsync();
            if (dbAvailable)
            {
                cred = await _db.AdminCredentials.AsNoTracking().FirstOrDefaultAsync();
            }
        }
        catch (DbException)
        {
            dbAvailable = false;
        }

        var username = cred?.Username ?? "admin";
        var password = cred?.Password ?? "admin";

        if (model.Username == username && model.Password == password)
        {
            HttpContext.Session.SetString("IsAdmin", "true");
            HttpContext.Session.SetString("AdminUser", username);
            if (!string.IsNullOrEmpty(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            return RedirectToAction("Index", "Admin");
        }

        model.ErrorMessage = "Invalid username or password";
        return View(model);
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Remove("IsAdmin");
        HttpContext.Session.Remove("AdminUser");
        return RedirectToAction("Index", "Home");
    }
}
