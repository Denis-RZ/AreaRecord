using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;
using MyWebApp.Models;
using MyWebApp.Services;
using MyWebApp.Filters;

namespace MyWebApp.Controllers.Admin;

[RoleAuthorize("Admin")]
[Route("Admin/Pages")] // base route
public class PagesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly LayoutService _layout;
    private readonly HtmlSanitizerService _sanitizer;

    public PagesController(ApplicationDbContext db, LayoutService layout, HtmlSanitizerService sanitizer)
    {
        _db = db;
        _layout = layout;
        _sanitizer = sanitizer;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var pages = await _db.Pages.AsNoTracking().OrderBy(p => p.Slug).ToListAsync();
        return View(pages);
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View(new Page());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Page model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        model.HeaderHtml = _sanitizer.Sanitize(model.HeaderHtml);
        model.BodyHtml = _sanitizer.Sanitize(model.BodyHtml);
        model.FooterHtml = _sanitizer.Sanitize(model.FooterHtml);
        if (model.IsPublished && model.PublishDate == null)
        {
            model.PublishDate = DateTime.UtcNow;
        }
        _db.Pages.Add(model);
        await _db.SaveChangesAsync();
        _layout.Reset();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(int id)
    {
        var page = await _db.Pages.FindAsync(id);
        if (page == null)
        {
            return NotFound();
        }
        ViewBag.Sections = await _db.PageSections.AsNoTracking()
            .Where(s => s.PageId == id)
            .OrderBy(s => s.Id)
            .ToListAsync();
        return View(page);
    }

    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Page model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Sections = await _db.PageSections.AsNoTracking()
                .Where(s => s.PageId == model.Id)
                .OrderBy(s => s.Id)
                .ToListAsync();
            return View(model);
        }
        model.HeaderHtml = _sanitizer.Sanitize(model.HeaderHtml);
        model.BodyHtml = _sanitizer.Sanitize(model.BodyHtml);
        model.FooterHtml = _sanitizer.Sanitize(model.FooterHtml);
        if (model.IsPublished && model.PublishDate == null)
        {
            model.PublishDate = DateTime.UtcNow;
        }
        _db.Update(model);
        await _db.SaveChangesAsync();
        _layout.Reset();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Delete/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var page = await _db.Pages.FindAsync(id);
        if (page == null)
        {
            return NotFound();
        }
        return View(page);
    }

    [HttpPost("Delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var page = await _db.Pages.FindAsync(id);
        if (page != null)
        {
            _db.Pages.Remove(page);
            await _db.SaveChangesAsync();
            _layout.Reset();
        }
        return RedirectToAction(nameof(Index));
    }

    // --- Section operations ---
    [HttpPost("AddSection")]
    public async Task<IActionResult> AddSection(PageSection model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        model.Html = _sanitizer.Sanitize(model.Html);
        _db.PageSections.Add(model);
        await _db.SaveChangesAsync();
        _layout.Reset();
        return Json(new { success = true, id = model.Id });
    }

    [HttpPost("RemoveSection/{id}")]
    public async Task<IActionResult> RemoveSection(int id)
    {
        var section = await _db.PageSections.FindAsync(id);
        if (section == null)
            return NotFound();

        _db.PageSections.Remove(section);
        await _db.SaveChangesAsync();
        _layout.Reset();
        return Json(new { success = true });
    }

    [HttpPost("DuplicateSection/{id}")]
    public async Task<IActionResult> DuplicateSection(int id)
    {
        var section = await _db.PageSections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (section == null)
            return NotFound();

        var copy = new PageSection
        {
            PageId = section.PageId,
            Area = section.Area,
            Html = section.Html
        };
        _db.PageSections.Add(copy);
        await _db.SaveChangesAsync();
        _layout.Reset();
        return Json(new { success = true, id = copy.Id });
    }

    [HttpPost("ReorderSection/{id}")]
    public async Task<IActionResult> ReorderSection(int id, string area)
    {
        var section = await _db.PageSections.FindAsync(id);
        if (section == null)
            return NotFound();
        section.Area = area;
        _db.Update(section);
        await _db.SaveChangesAsync();
        _layout.Reset();
        return Json(new { success = true });
    }
}
