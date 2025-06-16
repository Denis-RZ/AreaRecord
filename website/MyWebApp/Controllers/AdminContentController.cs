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

    public async Task<IActionResult> Index()
    {
        var pages = await _db.Pages.AsNoTracking().OrderBy(p => p.Slug).ToListAsync();
        return View(pages);
    }

    public IActionResult Create()
    {
        ViewBag.Sections = new List<PageSection>();
        return View("PageEditor", new Page());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Page model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Sections = new List<PageSection>();
            return View("PageEditor", model);
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

    public async Task<IActionResult> Edit(int id)
    {
        var page = await _db.Pages.FindAsync(id);
        if (page == null)
        {
            return NotFound();
        }
        var sections = await _db.PageSections.AsNoTracking()
            .Where(s => s.PageId == id)
            .OrderBy(s => s.Id)
            .ToListAsync();
        ViewBag.Sections = sections;
        return View("PageEditor", page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Page model)
    {
        if (!ModelState.IsValid)
        {
            var sections = await _db.PageSections.AsNoTracking()
                .Where(s => s.PageId == model.Id)
                .OrderBy(s => s.Id)
                .ToListAsync();
            ViewBag.Sections = sections;
            return View("PageEditor", model);
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
