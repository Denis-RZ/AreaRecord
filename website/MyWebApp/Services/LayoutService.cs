using Microsoft.EntityFrameworkCore;
using System.Linq;
using MyWebApp.Data;

namespace MyWebApp.Services;

public class LayoutService
{
    private readonly CacheService _cache;
    private readonly TokenRenderService _tokens;
    private const string HeaderKey = "layout_header";
    private const string FooterKey = "layout_footer";

    public static readonly Dictionary<string, string[]> LayoutZones = new()
    {
        ["single-column"] = new[] { "main" },
        ["two-column-sidebar"] = new[] { "main", "sidebar" }
    };

    public static bool IsValidArea(string layout, string area)
    {
        return LayoutZones.TryGetValue(layout, out var zones) && zones.Contains(area);
    }

    public static string[] GetAreas(string layout)
    {
        return LayoutZones.TryGetValue(layout, out var zones) ? zones : Array.Empty<string>();
    }

    public LayoutService(CacheService cache, TokenRenderService tokens)
    {
        _cache = cache;
        _tokens = tokens;
    }

    public async Task<string> GetHeaderAsync(ApplicationDbContext db)
    {
        return await _cache.GetOrCreateAsync(HeaderKey, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var parts = await db.PageSections.AsNoTracking()
                .Where(s => s.Page.Slug == "layout" && s.Area == "header")
                .OrderBy(s => s.SortOrder)
                .Select(s => s.Html)
                .ToListAsync();
            var html = string.Join(System.Environment.NewLine, parts);
            return await _tokens.RenderAsync(db, html);
        });
    }

    public async Task<string> GetFooterAsync(ApplicationDbContext db)
    {
        return await _cache.GetOrCreateAsync(FooterKey, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var parts = await db.PageSections.AsNoTracking()
                .Where(s => s.Page.Slug == "layout" && s.Area == "footer")
                .OrderBy(s => s.SortOrder)
                .Select(s => s.Html)
                .ToListAsync();
            var html = string.Join(System.Environment.NewLine, parts);
            return await _tokens.RenderAsync(db, html);
        });
    }

    public async Task<string> GetSectionAsync(ApplicationDbContext db, int pageId, string area)
    {
 
        var parts = await db.PageSections.AsNoTracking()
            .Where(s => s.PageId == pageId && s.Area == area)
            .OrderBy(s => s.SortOrder)
            .Select(s => s.Html)
            .ToListAsync();
        var html = string.Join(System.Environment.NewLine, parts);
        return await _tokens.RenderAsync(db, html);
 
    }

    public void Reset()
    {
        _cache.Remove(HeaderKey);
        _cache.Remove(FooterKey);
    }
}
