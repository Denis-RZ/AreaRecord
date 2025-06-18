using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using MyWebApp.Data;
using MyWebApp.Filters;
using MyWebApp.Models;
using MyWebApp.Services;

namespace MyWebApp.Controllers;

[RoleAuthorize("Admin")]
public class AdminBlockTemplateController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly HtmlSanitizerService _sanitizer;

    public AdminBlockTemplateController(ApplicationDbContext db, HtmlSanitizerService sanitizer)
    {
        _db = db;
        _sanitizer = sanitizer;
    }

    private async Task LoadPagesAsync()
    {
        ViewBag.Pages = await _db.Pages.AsNoTracking().OrderBy(p => p.Slug).ToListAsync();
        ViewBag.Roles = await _db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
    }

    public async Task<IActionResult> Index()
    {
        var items = await _db.BlockTemplates.AsNoTracking().OrderBy(t => t.Name).ToListAsync();
        return View(items);
    }

    public IActionResult Create()
    {
        return View(new BlockTemplate());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BlockTemplate model)
    {
        if (!ModelState.IsValid) return View(model);
        model.Html = _sanitizer.Sanitize(model.Html);
        _db.BlockTemplates.Add(model);
        _db.BlockTemplateVersions.Add(new BlockTemplateVersion { Template = model, Html = model.Html });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await _db.BlockTemplates.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(BlockTemplate model)
    {
        if (!ModelState.IsValid) return View(model);
        model.Html = _sanitizer.Sanitize(model.Html);
        _db.Update(model);
        _db.BlockTemplateVersions.Add(new BlockTemplateVersion { BlockTemplateId = model.Id, Html = model.Html });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.BlockTemplates.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await _db.BlockTemplates.FindAsync(id);
        if (item != null)
        {
            _db.BlockTemplates.Remove(item);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Html(int id)
    {
        var item = await _db.BlockTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (item == null) return NotFound();
        return Content(item.Html, "text/html");
    }

    public async Task<IActionResult> Export()
    {
        var list = await _db.BlockTemplates.AsNoTracking().ToListAsync();
        var json = JsonSerializer.Serialize(list);
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "blocks.json");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        if (file == null || file.Length == 0) return RedirectToAction(nameof(Index));
        using var reader = new StreamReader(file.OpenReadStream());
        var json = await reader.ReadToEndAsync();
        var list = JsonSerializer.Deserialize<List<BlockTemplate>>(json) ?? new();
        foreach (var t in list)
        {
            t.Id = 0;
            t.Html = _sanitizer.Sanitize(t.Html);
            _db.BlockTemplates.Add(t);
            _db.BlockTemplateVersions.Add(new BlockTemplateVersion { Template = t, Html = t.Html });
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetBlocks()
    {
        var items = await _db.BlockTemplates.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new { t.Id, t.Name, Preview = t.Html.Length > 200 ? t.Html.Substring(0, 200) + "..." : t.Html })
            .ToListAsync();
        return Json(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFromSection(string name, string html)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(html))
            return BadRequest();
        html = _sanitizer.Sanitize(html);
        var t = new BlockTemplate { Name = name, Html = html };
        _db.BlockTemplates.Add(t);
        _db.BlockTemplateVersions.Add(new BlockTemplateVersion { Template = t, Html = html });
        await _db.SaveChangesAsync();
        return Json(new { t.Id });
    }

    public async Task<IActionResult> AddToPage(int id)
    {
        var item = await _db.BlockTemplates.FindAsync(id);
        if (item == null) return NotFound();
        await LoadPagesAsync();
        ViewBag.BlockId = id;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToPage(int id, List<int> pageIds, string zone, string role)
    {
        var template = await _db.BlockTemplates.FindAsync(id);
        if (template == null) return NotFound();
        if (pageIds == null || pageIds.Count == 0)
        {
            await LoadPagesAsync();
            ViewBag.BlockId = id;
            ModelState.AddModelError("pageIds", "Page selection required");
            return View();
        }
        var roleEntity = await _db.Roles.FirstOrDefaultAsync(r => r.Name == role);
        int? roleId = roleEntity?.Id;
        zone = zone?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(zone))
        {
            await LoadPagesAsync();
            ViewBag.BlockId = id;
            ModelState.AddModelError("zone", "Zone required");
            return View();
        }
        if (pageIds.Contains(0))
        {
            pageIds = await _db.Pages.Select(p => p.Id).ToListAsync();
        }
        foreach (var pageId in pageIds)
        {
            var sort = await _db.PageSections
                .Where(s => s.PageId == pageId && s.Zone == zone)
                .Select(s => s.SortOrder)
                .DefaultIfEmpty(-1)
                .MaxAsync() + 1;
            var section = new PageSection
            {
                PageId = pageId,
                Zone = zone,
                SortOrder = sort,
                Html = template.Html,
                Type = PageSectionType.Html,
                RoleId = roleId
            };
            _db.PageSections.Add(section);
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

}
