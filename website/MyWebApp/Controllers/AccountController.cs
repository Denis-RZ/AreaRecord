using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;
using MyWebApp.Models;
using MyWebApp.Services;
using System.Data.Common;

namespace MyWebApp.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly RecaptchaService _recaptcha;
    private readonly IEmailSender _emailSender;

    public AccountController(ApplicationDbContext db, RecaptchaService recaptcha, IEmailSender emailSender)
    {
        _db = db;
        _recaptcha = recaptcha;
        _emailSender = emailSender;
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

        var captchaResponse = Request.Form["g-recaptcha-response"].ToString();
        if (!await _recaptcha.VerifyAsync(captchaResponse))
        {
            model.ErrorMessage = "CAPTCHA validation failed.";
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

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string username)
    {
        var captcha = Request.Form["g-recaptcha-response"].ToString();
        if (!await _recaptcha.VerifyAsync(captcha))
        {
            ModelState.AddModelError(string.Empty, "CAPTCHA validation failed.");
            return View();
        }

        var expired = _db.PasswordResetTokens.Where(t => t.Expiration < DateTime.UtcNow || t.Used);
        if (expired.Any())
        {
            _db.PasswordResetTokens.RemoveRange(expired);
            await _db.SaveChangesAsync();
        }

        var cred = await _db.AdminCredentials.FirstOrDefaultAsync();
        if (cred != null && string.Equals(username, cred.Username, StringComparison.OrdinalIgnoreCase))
        {
            var token = new PasswordResetToken
            {
                AdminCredentialId = cred.Id,
                Token = Guid.NewGuid().ToString("N"),
                Expiration = DateTime.UtcNow.AddHours(1),
                Used = false
            };
            _db.PasswordResetTokens.Add(token);
            await _db.SaveChangesAsync();

            var link = Url.Action(nameof(ResetPassword), "Account", new { token = token.Token }, Request.Scheme);
            await _emailSender.SendEmailAsync(cred.Username, "Password Reset", $"Reset your password: {link}");
        }

        ViewBag.Message = "If the account exists, a recovery link was sent.";
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string token)
    {
        var reset = await _db.PasswordResetTokens
            .Include(p => p.AdminCredential)
            .FirstOrDefaultAsync(t => t.Token == token && !t.Used && t.Expiration > DateTime.UtcNow);
        if (reset == null)
            return RedirectToAction(nameof(Login));
        ViewBag.Token = token;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string token, string password)
    {
        var captcha = Request.Form["g-recaptcha-response"].ToString();
        if (!await _recaptcha.VerifyAsync(captcha))
        {
            ModelState.AddModelError(string.Empty, "CAPTCHA validation failed.");
            ViewBag.Token = token;
            return View();
        }

        var reset = await _db.PasswordResetTokens
            .Include(p => p.AdminCredential)
            .FirstOrDefaultAsync(t => t.Token == token && !t.Used && t.Expiration > DateTime.UtcNow);
        if (reset == null)
            return RedirectToAction(nameof(Login));

        if (reset.AdminCredential != null)
        {
            reset.AdminCredential.Password = password;
            reset.Used = true;
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Login));
    }
}
