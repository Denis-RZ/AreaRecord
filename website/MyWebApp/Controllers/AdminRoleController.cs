using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;
using MyWebApp.Filters;
using MyWebApp.Models;
using System.Linq;
using System.Threading.Tasks;

namespace MyWebApp.Controllers;

[RoleAuthorize("Admin")]
public class AdminRoleController : Controller
{
    private readonly ApplicationDbContext _db;

    public AdminRoleController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var roles = await _db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
        return View(roles);
    }

    public IActionResult Create()
    {
        return View(new Role());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Role model)
    {
        if (!ModelState.IsValid) return View(model);
        _db.Roles.Add(model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var role = await _db.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound();
        var permissions = await _db.Permissions.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        var vm = new RoleEditViewModel
        {
            Role = role,
            SelectedPermissions = role.Permissions.Select(p => p.PermissionId).ToList()
        };
        ViewBag.Permissions = permissions;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(RoleEditViewModel model)
    {
        var role = await _db.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == model.Role.Id);
        if (role == null) return NotFound();
        role.Name = model.Role.Name;
        _db.RolePermissions.RemoveRange(role.Permissions);
        role.Permissions.Clear();
        foreach (var pid in model.SelectedPermissions.Distinct())
        {
            role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionId = pid });
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null) return NotFound();
        return View(role);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role != null)
        {
            _db.Roles.Remove(role);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
