using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using MyWebApp.Data;
using System.Collections.Generic;
using MyWebApp.Filters;
using MyWebApp.Models;
using MyWebApp.Services;

namespace MyWebApp.Controllers;

[RoleAuthorize("Admin")]
public class AdminContentController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly LayoutService _layout;
    private readonly HtmlSanitizerService _sanitizer;

    public AdminContentController(ApplicationDbContext db, LayoutService layout, HtmlSanitizerService sanitizer)
    {
        _db = db;
        _layout = layout;
        _sanitizer = sanitizer;
    }

    private async Task LoadTemplatesAsync()
    {
        ViewBag.Templates = await _db.BlockTemplates.AsNoTracking()
            .OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<IActionResult> Index()
    {
        var pages = await _db.Pages.AsNoTracking().OrderBy(p => p.Slug).ToListAsync();
        return View(pages);
    }

    public async Task<IActionResult> Create()
    {
 
        await LoadTemplatesAsync();
        ViewBag.Sections = new List<PageSection>();
        return View("PageEditor", new Page());
 
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Page model)
    {
        if (!ModelState.IsValid)
        {
            await LoadTemplatesAsync();
            ViewBag.Sections = model.Sections;
            return View("PageEditor", model);
        }
        if (model.IsPublished && model.PublishDate == null)
        {
            model.PublishDate = DateTime.UtcNow;
        }
        _db.Pages.Add(model);
        await _db.SaveChangesAsync();
        if (model.Sections != null && model.Sections.Count > 0)
        {
            foreach (var s in model.Sections)
            {
                s.PageId = model.Id;
                s.Html = _sanitizer.Sanitize(s.Html);
                _db.PageSections.Add(s);
            }
            await _db.SaveChangesAsync();
        }
        _layout.Reset();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var page = await _db.Pages.FindAsync(id);
        if (page == null)
        {
            return NotFound();
        }

        await LoadTemplatesAsync();
        ViewBag.Sections = await _db.PageSections.Where(s => s.PageId == id)
            .OrderBy(s => s.SortOrder).ToListAsync();
        return View("PageEditor", page);
 
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Page model)
    {
        if (!ModelState.IsValid)
        {
            await LoadTemplatesAsync();
            ViewBag.Sections = model.Sections;
            return View("PageEditor", model);
        }
        if (model.IsPublished && model.PublishDate == null)
        {
            model.PublishDate = DateTime.UtcNow;
        }
        _db.Update(model);
        await _db.SaveChangesAsync();
        if (model.Sections != null && model.Sections.Count > 0)
        {
            var existing = _db.PageSections.Where(s => s.PageId == model.Id);
            _db.PageSections.RemoveRange(existing);
            foreach (var s in model.Sections)
            {
                s.PageId = model.Id;
                s.Html = _sanitizer.Sanitize(s.Html);
                _db.PageSections.Add(s);
            }
            await _db.SaveChangesAsync();
        }
        _layout.Reset();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var page = await _db.Pages.FindAsync(id);
        if (page == null)
        {
            return NotFound();
        }
        return View(page);
    }

    [HttpPost, ActionName("Delete")]
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
}
