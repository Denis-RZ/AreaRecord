using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using MyWebApp.Data;
using System.Collections.Generic;
using MyWebApp.Filters;
using MyWebApp.Models;
using MyWebApp.Services;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace MyWebApp.Controllers;

[RoleAuthorize("Admin")]
public class AdminContentController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly LayoutService _layout;
    private readonly ContentProcessingService _content;

    public AdminContentController(ApplicationDbContext db, LayoutService layout, ContentProcessingService content)
    {
        _db = db;
        _layout = layout;
        _content = content;
    }

    private async Task LoadTemplatesAsync()
    {
        ViewBag.Templates = await _db.BlockTemplates.AsNoTracking()
            .OrderBy(t => t.Name).ToListAsync();
        ViewBag.Permissions = await _db.Permissions.AsNoTracking()
            .OrderBy(p => p.Name).ToListAsync();
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
        var sections = model.Sections?.ToList() ?? new List<PageSection>();
        if (sections.Any(s => !LayoutService.IsValidZone(model.Layout, s.Zone)))
        {
            ModelState.AddModelError(string.Empty, "Invalid area for selected layout.");
        }
        if (!sections.Any(s => s.Zone == "main"))
        {
            ModelState.AddModelError(string.Empty, "Main area cannot be empty.");
        }
        if (!ModelState.IsValid)
        {
            await LoadTemplatesAsync();
            ViewBag.Sections = sections;
            model.Sections = sections;
            return View("PageEditor", model);
        }
        model.Sections = new List<PageSection>();
        _db.Pages.Add(model);
        await _db.SaveChangesAsync();
        if (sections.Count > 0)
        {
            var files = HttpContext.Request.Form.Files;
            for (int i = 0; i < sections.Count; i++)
            {
                var s = sections[i];
                s.Id = 0;
                s.PageId = model.Id;
                var file = files.FirstOrDefault(f => f.Name == $"Sections[{i}].File");
                await _content.PrepareHtmlAsync(s, file);
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
        var sections = model.Sections?.ToList() ?? new List<PageSection>();
        if (sections.Any(s => !LayoutService.IsValidZone(model.Layout, s.Zone)))
        {
            ModelState.AddModelError(string.Empty, "Invalid area for selected layout.");
        }
        if (!sections.Any(s => s.Zone == "main"))
        {
            ModelState.AddModelError(string.Empty, "Main area cannot be empty.");
        }
        if (!ModelState.IsValid)
        {
            await LoadTemplatesAsync();
            ViewBag.Sections = sections;
            model.Sections = sections;
            return View("PageEditor", model);
        }
        model.Sections = new List<PageSection>();
        _db.Update(model);
        await _db.SaveChangesAsync();
        var existing = _db.PageSections.Where(s => s.PageId == model.Id);
        _db.PageSections.RemoveRange(existing);
        if (sections.Count > 0)
        {
            var files = HttpContext.Request.Form.Files;
            for (int i = 0; i < sections.Count; i++)
            {
                var s = sections[i];
                s.Id = 0;
                s.PageId = model.Id;
                var file = files.FirstOrDefault(f => f.Name == $"Sections[{i}].File");
                await _content.PrepareHtmlAsync(s, file);
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
