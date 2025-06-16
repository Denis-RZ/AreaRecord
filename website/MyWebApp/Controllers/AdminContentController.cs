using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using MyWebApp.Data;
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
        return View(new Page());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Page model)
    {
        if (!ModelState.IsValid)
        {
            await LoadTemplatesAsync();
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

    public async Task<IActionResult> Edit(int id)
    {
        var page = await _db.Pages.FindAsync(id);
        if (page == null)
        {
            return NotFound();
        }
        await LoadTemplatesAsync();
        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Page model)
    {
        if (!ModelState.IsValid)
        {
            await LoadTemplatesAsync();
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
