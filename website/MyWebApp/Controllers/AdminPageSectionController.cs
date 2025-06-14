using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using MyWebApp.Data;
using MyWebApp.Filters;
using MyWebApp.Models;
using MyWebApp.Services;

namespace MyWebApp.Controllers;

[BasicAuth]
public class AdminPageSectionController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly LayoutService _layout;

    public AdminPageSectionController(ApplicationDbContext db, LayoutService layout)
    {
        _db = db;
        _layout = layout;
    }

    public async Task<IActionResult> Index()
    {
        var sections = await _db.PageSections.AsNoTracking()
            .Include(s => s.Page)
            .OrderBy(s => s.Page.Slug).ThenBy(s => s.Area)
            .ToListAsync();
        return View(sections);
    }

    private async Task LoadPagesAsync()
    {
        ViewBag.Pages = await _db.Pages.AsNoTracking().OrderBy(p => p.Slug).ToListAsync();
    }

    public async Task<IActionResult> Create()
    {
        await LoadPagesAsync();
        return View(new PageSection());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PageSection model)
    {
        if (!ModelState.IsValid)
        {
            await LoadPagesAsync();
            return View(model);
        }
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
    public async Task<IActionResult> Edit(PageSection model)
    {
        if (!ModelState.IsValid)
        {
            await LoadPagesAsync();
            return View(model);
        }
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
