using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;
using MyWebApp.Models;
using MyWebApp.Services;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System;

namespace MyWebApp.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly CaptchaService _captchaService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AccountController> _logger;
    private static readonly Dictionary<string, (int Count, DateTime LockoutEnd)> _attempts = new();

    private bool HasRole(string role)
    {
        var roles = HttpContext.Session.GetString("Roles");
        var roleNames = string.IsNullOrWhiteSpace(roles) ? new[] { "Anonym" } : roles.Split(',');
        return roleNames.Contains(role);
    }

    public AccountController(ApplicationDbContext db, CaptchaService captchaService, IEmailSender emailSender, ILogger<AccountController> logger)
    {
        _db = db;
        _captchaService = captchaService;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        _captchaService.CreateChallenge();
        ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
        var remember = Request.Cookies["RememberMe"];
        string user = string.Empty;
        if (!string.IsNullOrEmpty(remember))
        {
            try
            {
                var data = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(remember));
                var parts = data.Split(':', 2);
                if (parts.Length == 2)
                {
                    user = parts[0];
                    var pass = parts[1];
                    AdminCredential? cred = null;
                    bool dbAvailable = true;
                    try
                    {
                        dbAvailable = await _db.Database.CanConnectAsync();
                        if (dbAvailable)
                        {
                            cred = await _db.AdminCredentials.AsNoTracking()
                                .FirstOrDefaultAsync(c => c.Username == user);
                        }
                    }
                    catch (DbException)
                    {
                        dbAvailable = false;
                    }
                    var username = cred?.Username ?? "admin";
                    var password = cred?.Password ?? "admin";
                    if (user == username && pass == password)
                    {
                        HttpContext.Session.SetString("IsAdmin", "true");
                        HttpContext.Session.SetString("AdminUser", username);
                        HttpContext.Session.SetString("Roles", "Admin");
                        if (!string.IsNullOrEmpty(returnUrl))
                            return Redirect(returnUrl);
                        return RedirectToAction("Index", "Admin");
                    }
                }
            }
            catch { }
        }
        return View(new LoginViewModel { ReturnUrl = returnUrl, Username = user, RememberMe = !string.IsNullOrEmpty(user) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        var captchaResponse = Request.Form["captcha"].ToString();
        if (!_captchaService.Validate(captchaResponse))
        {
            model.ErrorMessage = "CAPTCHA validation failed.";
            _captchaService.CreateChallenge();
            ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            model.ErrorMessage = "Invalid data";
            _captchaService.CreateChallenge();
            ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
            return View(model);
        }

        if (_attempts.TryGetValue(model.Username, out var info) && info.LockoutEnd > DateTime.UtcNow)
        {
            model.ErrorMessage = "Account locked. Try again later.";
            _captchaService.CreateChallenge();
            ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
            return View(model);
        }

        AdminCredential? cred = null;
        bool dbAvailable = true;
        try
        {
            dbAvailable = await _db.Database.CanConnectAsync();
            if (dbAvailable)
            {
                cred = await _db.AdminCredentials.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Username == model.Username);
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
            _logger.LogInformation("User {User} logged in successfully", model.Username);
            HttpContext.Session.SetString("IsAdmin", "true");
            HttpContext.Session.SetString("AdminUser", username);
            HttpContext.Session.SetString("Roles", "Admin");
            _attempts.Remove(model.Username);
            if (model.RememberMe)
            {
                var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
                Response.Cookies.Append("RememberMe", token, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddDays(7), HttpOnly = true, IsEssential = true });
            }
            else
            {
                Response.Cookies.Delete("RememberMe");
            }
            if (!string.IsNullOrEmpty(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            return RedirectToAction("Index", "Admin");
        }
        _logger.LogWarning("Failed login for {User}", model.Username);
        if (_attempts.TryGetValue(model.Username, out info))
        {
            info.Count++;
        }
        else
        {
            info = (1, DateTime.MinValue);
        }
        if (info.Count >= 5)
        {
            info = (0, DateTime.UtcNow.AddMinutes(15));
        }
        _attempts[model.Username] = info;
        model.ErrorMessage = "Invalid username or password";
        _captchaService.CreateChallenge();
        ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
        return View(model);
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Remove("IsAdmin");
        HttpContext.Session.Remove("AdminUser");
        HttpContext.Session.Remove("Roles");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        _captchaService.CreateChallenge();
        ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string username)
    {
        var captcha = Request.Form["captcha"].ToString();
        if (!_captchaService.Validate(captcha))
        {
            ModelState.AddModelError(string.Empty, "CAPTCHA validation failed.");
            _captchaService.CreateChallenge();
            ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
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
            var body = $"<p>Click <a href='{link}'>here</a> to reset your password.</p>";
            await _emailSender.SendEmailAsync(cred.Username, "Password Reset", body);
        }

        ViewBag.Message = "If the account exists, a recovery link was sent.";
        _captchaService.CreateChallenge();
        ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
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
        _captchaService.CreateChallenge();
        ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string token, string password)
    {
        var captcha = Request.Form["captcha"].ToString();
        if (!_captchaService.Validate(captcha))
        {
            ModelState.AddModelError(string.Empty, "CAPTCHA validation failed.");
            ViewBag.Token = token;
            _captchaService.CreateChallenge();
            ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
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

    [HttpGet]
    public IActionResult Register()
    {
        _captchaService.CreateChallenge();
        ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        var captcha = Request.Form["captcha"].ToString();
        if (!_captchaService.Validate(captcha))
        {
            model.ErrorMessage = "CAPTCHA validation failed.";
            _captchaService.CreateChallenge();
            ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(model.Username) ||
            string.IsNullOrWhiteSpace(model.Password) ||
            model.Password != model.ConfirmPassword ||
            string.IsNullOrWhiteSpace(model.Email) ||
            !model.AcceptTerms || !model.AcceptPrivacy)
        {
            model.ErrorMessage = "Invalid data";
            _captchaService.CreateChallenge();
            ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
            return View(model);
        }

        if (await _db.SiteUsers.AnyAsync(u => u.Username == model.Username || u.Email == model.Email))
        {
            model.ErrorMessage = "User already exists.";
            _captchaService.CreateChallenge();
            ViewBag.CaptchaToken = DateTime.UtcNow.Ticks;
            return View(model);
        }

        if (!(HasRole("Admin") || HasRole("Moderator")))
        {
            model.AccountType = "User";
        }

        var user = new SiteUser
        {
            Username = model.Username,
            Password = model.Password,
            FirstName = model.FirstName,
            LastName = model.LastName,
            Email = model.Email,
            PhoneNumber = model.PhoneNumber,
            DateOfBirth = model.DateOfBirth,
            AcceptTerms = model.AcceptTerms,
            AcceptPrivacy = model.AcceptPrivacy,
            AccountType = model.AccountType,
            EmailVerified = false
        };
        _db.SiteUsers.Add(user);
        await _db.SaveChangesAsync();

        var token = new EmailVerificationToken
        {
            SiteUserId = user.Id,
            Token = Guid.NewGuid().ToString("N"),
            Expiration = DateTime.UtcNow.AddHours(24),
            Used = false
        };
        _db.EmailVerificationTokens.Add(token);
        await _db.SaveChangesAsync();

        var link = Url.Action(nameof(VerifyEmail), "Account", new { token = token.Token }, Request.Scheme);
        var body = $"<p>Click <a href='{link}'>here</a> to verify your email.</p>";
        await _emailSender.SendEmailAsync(model.Email, "Verify Email", body);

        _logger.LogInformation("New user {User} registered", model.Username);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> VerifyEmail(string token)
    {
        var record = await _db.EmailVerificationTokens.Include(t => t.SiteUser)
            .FirstOrDefaultAsync(t => t.Token == token && !t.Used && t.Expiration > DateTime.UtcNow);
        if (record?.SiteUser == null)
            return RedirectToAction(nameof(Login));

        record.Used = true;
        record.SiteUser.EmailVerified = true;
        await _db.SaveChangesAsync();
        return View();
    }
}
