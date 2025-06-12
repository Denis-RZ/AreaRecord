using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;
using MyWebApp.Filters;
using MyWebApp.Models;

namespace MyWebApp.Controllers;

[BasicAuth]
public class FilesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FilesController> _logger;

    public FilesController(ApplicationDbContext context, ILogger<FilesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var files = await _context.DownloadFiles.AsNoTracking().ToListAsync();
        var model = new List<FileStatsViewModel>();
        foreach (var f in files)
        {
            var count = await _context.Downloads.AsNoTracking().CountAsync(d => d.DownloadFileId == f.Id && d.IsSuccessful);
            model.Add(new FileStatsViewModel { File = f, DownloadCount = count });
        }
        return View(model);
    }

    public IActionResult Create()
    {
        return View(new DownloadFile { Created = DateTime.UtcNow });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DownloadFile file)
    {
        if (ModelState.IsValid)
        {
            file.Created = DateTime.UtcNow;
            _context.DownloadFiles.Add(file);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(file);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var file = await _context.DownloadFiles.FindAsync(id);
        if (file == null) return NotFound();
        return View(file);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DownloadFile file)
    {
        if (ModelState.IsValid)
        {
            _context.Update(file);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(file);
    }

    public async Task<IActionResult> Delete(int id)
    {
        var file = await _context.DownloadFiles.FindAsync(id);
        if (file == null) return NotFound();
        return View(file);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var file = await _context.DownloadFiles.FindAsync(id);
        if (file != null)
        {
            _context.DownloadFiles.Remove(file);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
