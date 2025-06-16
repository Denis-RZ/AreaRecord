using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
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
        _db.BlockTemplateVersions.Add(new BlockTemplateVersion { BlockTemplate = model, Html = model.Html });
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
            _db.BlockTemplateVersions.Add(new BlockTemplateVersion { BlockTemplate = t, Html = t.Html });
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
