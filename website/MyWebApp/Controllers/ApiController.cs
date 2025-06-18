using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;
using MyWebApp.Filters;
using MyWebApp.Services;

namespace MyWebApp.Controllers;

[RoleAuthorize("Admin")]
public class ApiController : Controller
{
    private readonly ApplicationDbContext _db;

    public ApiController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetBlocks()
    {
        var items = await _db.BlockTemplates.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();
        return Json(items);
    }

    [HttpGet]
    public async Task<IActionResult> GetPages()
    {
        var pages = await _db.Pages.AsNoTracking()
            .OrderBy(p => p.Slug)
            .Select(p => new { p.Id, p.Slug })
            .ToListAsync();
        return Json(pages);
    }

    [HttpGet]
    public async Task<IActionResult> GetSections(int id)
    {
        var zones = await _db.PageSections.AsNoTracking()
            .Where(s => s.PageId == id)
            .Select(s => s.Zone)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();
        return Json(zones);
    }

    [HttpGet]
    public async Task<IActionResult> GetZonesForPage(int id)
    {
        var layout = await _db.Pages.Where(p => p.Id == id).Select(p => p.Layout).FirstOrDefaultAsync() ?? "single-column";
        var zones = LayoutService.GetZones(layout);
        return Json(zones);
    }
}
