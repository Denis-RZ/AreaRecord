using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.IO;
using MyWebApp.Data;
using MyWebApp.Filters;
using MyWebApp.Models;

namespace MyWebApp.Controllers;

[RoleAuthorize("Admin")]
public class MediaController : Controller
{
    private readonly ApplicationDbContext _db;

    public MediaController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var items = await _db.MediaItems.AsNoTracking().OrderBy(m => m.FileName).ToListAsync();
        return View(items);
    }

    public IActionResult Upload()
    {
        return View(new Media());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(Media model, IFormFile? file)
    {
        if (file != null && file.Length > 0)
        {
            var uploads = Path.Combine("wwwroot", "uploads");
            Directory.CreateDirectory(uploads);
            var fileName = Path.GetFileName(file.FileName);
            var filePath = Path.Combine(uploads, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);
            model.FileName = fileName;
            model.FilePath = $"/uploads/{fileName}";
            model.ContentType = file.ContentType;
            model.Size = file.Length;
            _db.MediaItems.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        ModelState.AddModelError(string.Empty, "File is required");
        return View(model);
    }
}
