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

        var header = await _layout.GetSectionAsync(Db, page.Id, "header");
        if (string.IsNullOrEmpty(header))
        {
            header = await _layout.GetHeaderAsync(Db);
        }
        var footer = await _layout.GetSectionAsync(Db, page.Id, "footer");
        if (string.IsNullOrEmpty(footer))
        {
            footer = await _layout.GetFooterAsync(Db);
        }

        var layoutName = string.IsNullOrWhiteSpace(page.Layout) ? "single-column" : page.Layout;
        if (!LayoutService.LayoutZones.TryGetValue(layoutName, out var zones))
        {
            zones = LayoutService.LayoutZones["single-column"];
        }
        var zoneHtml = new Dictionary<string, string>();
        foreach (var z in zones)
        {
            var html = await _layout.GetSectionAsync(Db, page.Id, z);
            if (z == "main")
            {
                html = page.BodyHtml + html;
            }
            zoneHtml[z] = html;
        }

        ViewBag.HeaderHtml = header;
        ViewBag.FooterHtml = footer;
        ViewBag.PageLayout = layoutName;
        ViewBag.ZoneHtml = zoneHtml;
        return View(page);
    }
}
