using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Linq;
using MyWebApp.Data;

namespace MyWebApp.Services;

public class LayoutService
{
    private readonly CacheService _cache;
    private readonly TokenRenderService _tokens;
    private readonly IHttpContextAccessor _accessor;
    private const string HeaderKey = "layout_header";
    private const string FooterKey = "layout_footer";

    public static readonly Dictionary<string, string[]> LayoutZones = new()
    {
        ["single-column"] = new[] { "main" },
        ["two-column-sidebar"] = new[] { "main", "sidebar" }
    };

    public static bool IsValidZone(string layout, string zone)
    {
        return LayoutZones.TryGetValue(layout, out var zones) && zones.Contains(zone);
    }

    public static string[] GetZones(string layout)
    {
        return LayoutZones.TryGetValue(layout, out var zones) ? zones : Array.Empty<string>();
    }

    public LayoutService(CacheService cache, TokenRenderService tokens, IHttpContextAccessor accessor)
    {
        _cache = cache;
        _tokens = tokens;
        _accessor = accessor;
    }

    private string[] GetRoles()
    {
        var roles = _accessor.HttpContext?.Session.GetString("Roles");
        return string.IsNullOrWhiteSpace(roles) ? Array.Empty<string>() : roles.Split(',');
    }

    private async Task<List<int>> GetAllowedPermissionsAsync(ApplicationDbContext db, string[] roles)
    {
        if (roles.Length == 0) return new List<int>();
        return await db.RolePermissions.AsNoTracking()
            .Where(rp => roles.Contains(rp.Role!.Name))
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<string> GetHeaderAsync(ApplicationDbContext db)
    {
        var roles = GetRoles();
        if (roles.Length == 0)
        {
            return await _cache.GetOrCreateAsync(HeaderKey, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                var parts = await db.PageSections.AsNoTracking()
                    .Where(s => s.Page.Slug == "layout" && s.Zone == "header" && s.PermissionId == null)
                    .OrderBy(s => s.SortOrder)
                    .Select(s => s.Html)
                    .ToListAsync();
                var html = string.Join(System.Environment.NewLine, parts);
                return await _tokens.RenderAsync(db, html);
            });
        }

        var allowed = await GetAllowedPermissionsAsync(db, roles);
        var query = db.PageSections.AsNoTracking()
            .Where(s => s.Page.Slug == "layout" && s.Zone == "header");
        query = query.Where(s => s.PermissionId == null || allowed.Contains(s.PermissionId.Value));
        var parts2 = await query.OrderBy(s => s.SortOrder).Select(s => s.Html).ToListAsync();
        var html2 = string.Join(System.Environment.NewLine, parts2);
        return await _tokens.RenderAsync(db, html2);
    }

    public async Task<string> GetFooterAsync(ApplicationDbContext db)
    {
        var roles = GetRoles();
        if (roles.Length == 0)
        {
            return await _cache.GetOrCreateAsync(FooterKey, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                var parts = await db.PageSections.AsNoTracking()
                    .Where(s => s.Page.Slug == "layout" && s.Zone == "footer" && s.PermissionId == null)
                    .OrderBy(s => s.SortOrder)
                    .Select(s => s.Html)
                    .ToListAsync();
                var html = string.Join(System.Environment.NewLine, parts);
                return await _tokens.RenderAsync(db, html);
            });
        }

        var allowed = await GetAllowedPermissionsAsync(db, roles);
        var query = db.PageSections.AsNoTracking()
            .Where(s => s.Page.Slug == "layout" && s.Zone == "footer");
        query = query.Where(s => s.PermissionId == null || allowed.Contains(s.PermissionId.Value));
        var parts2 = await query.OrderBy(s => s.SortOrder).Select(s => s.Html).ToListAsync();
        var html2 = string.Join(System.Environment.NewLine, parts2);
        return await _tokens.RenderAsync(db, html2);
    }

    public async Task<string> GetSectionAsync(ApplicationDbContext db, int pageId, string zone)
    {
        var roles = GetRoles();
        var allowed = await GetAllowedPermissionsAsync(db, roles);
        var query = db.PageSections.AsNoTracking()
            .Where(s => s.PageId == pageId && s.Zone == zone);
        if (allowed.Count == 0)
            query = query.Where(s => s.PermissionId == null);
        else
            query = query.Where(s => s.PermissionId == null || allowed.Contains(s.PermissionId.Value));
        var parts = await query.OrderBy(s => s.SortOrder).Select(s => s.Html).ToListAsync();
        var html = string.Join(System.Environment.NewLine, parts);
        return await _tokens.RenderAsync(db, html);

    }

    public void Reset()
    {
        _cache.Remove(HeaderKey);
        _cache.Remove(FooterKey);
    }
}
