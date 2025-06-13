using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;
using MyWebApp.Models;
using MyWebApp.Services;

namespace MyWebApp.Controllers;

public class PagesController : BaseController
{
    private readonly LayoutService _layout;

    public PagesController(ApplicationDbContext db, ILogger<PagesController> logger, LayoutService layout)
        : base(db, logger)
    {
        _layout = layout;
    }

    public async Task<IActionResult> Show(string? slug)
    {
        if (!CheckDatabase())
        {
            return RedirectToSetup();
        }

        slug = string.IsNullOrWhiteSpace(slug) ? "home" : slug.ToLowerInvariant();
        var page = await Db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug);
        if (page == null)
        {
            return NotFound();
        }

        ViewBag.HeaderHtml = await _layout.GetHeaderAsync(Db);
        ViewBag.FooterHtml = await _layout.GetFooterAsync(Db);
        return View(page);
    }
}
