using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using MyWebApp.Data;
using MyWebApp.Filters;
using MyWebApp.Models;
using MyWebApp.Services;

namespace MyWebApp.Controllers;

[RoleAuthorize("Admin")]
public class AdminPageSectionController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly LayoutService _layout;
    private readonly ContentProcessingService _content;

    public AdminPageSectionController(ApplicationDbContext db, LayoutService layout, ContentProcessingService content)
    {
        _db = db;
        _layout = layout;
        _content = content;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var query = _db.PageSections.AsNoTracking().Include(s => s.Page).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.ToLowerInvariant();
            query = query.Where(s => s.Zone.ToLower().Contains(q) || s.Html.ToLower().Contains(q) || s.Page.Slug.ToLower().Contains(q));
        }
        var sections = await query.OrderBy(s => s.Page.Slug).ThenBy(s => s.Zone).ToListAsync();
        ViewBag.Query = q;
        return View(sections);
    }

    private async Task LoadPagesAsync()
    {
        ViewBag.Pages = await _db.Pages.AsNoTracking().OrderBy(p => p.Slug).ToListAsync();
        ViewBag.Permissions = await _db.Permissions.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        ViewBag.Roles = await _db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
    }

    public async Task<IActionResult> Create()
    {
        await LoadPagesAsync();
        return View(new PageSection());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PageSection model, IFormFile? file)
    {
        if (!ModelState.IsValid)
        {
            await LoadPagesAsync();
            return View(model);
        }
        if (!ModelState.IsValid)
        {
            await LoadPagesAsync();
            return View(model);
        }
        await _content.PrepareHtmlAsync(model, file);
        _db.PageSections.Add(model);
        await _db.SaveChangesAsync();
        _layout.Reset();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var section = await _db.PageSections.FindAsync(id);
        if (section == null) return NotFound();
        await LoadPagesAsync();
        return View(section);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PageSection model, IFormFile? file)
    {
        if (!ModelState.IsValid)
        {
            await LoadPagesAsync();
            return View(model);
        }
        if (!ModelState.IsValid)
        {
            await LoadPagesAsync();
            return View(model);
        }
        await _content.PrepareHtmlAsync(model, file);
        _db.Update(model);
        await _db.SaveChangesAsync();
        _layout.Reset();
        return RedirectToAction(nameof(Index));
    }


    public async Task<IActionResult> Delete(int id)
    {
        var section = await _db.PageSections.FindAsync(id);
        if (section == null) return NotFound();
        return View(section);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var section = await _db.PageSections.FindAsync(id);
        if (section != null)
        {
            _db.PageSections.Remove(section);
            await _db.SaveChangesAsync();
            _layout.Reset();
        }
        return RedirectToAction(nameof(Index));
    }

}
