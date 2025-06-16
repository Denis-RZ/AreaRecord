using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;

namespace MyWebApp.Services;

public class LayoutService
{
    private readonly CacheService _cache;
    private const string HeaderKey = "layout_header";
    private const string FooterKey = "layout_footer";

    public static readonly Dictionary<string, string[]> LayoutZones = new()
    {
        ["single-column"] = new[] { "main" },
        ["two-column-sidebar"] = new[] { "main", "sidebar" }
    };

    public LayoutService(CacheService cache)
    {
        _cache = cache;
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
            return string.Join(System.Environment.NewLine, parts);
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
            return string.Join(System.Environment.NewLine, parts);
        });
    }

    public async Task<string> GetSectionAsync(ApplicationDbContext db, int pageId, string area)
    {
 
        var parts = await db.PageSections.AsNoTracking()
            .Where(s => s.PageId == pageId && s.Area == area)
            .OrderBy(s => s.SortOrder)
            .Select(s => s.Html)
            .ToListAsync();
        return string.Join(System.Environment.NewLine, parts);
 
    }

    public void Reset()
    {
        _cache.Remove(HeaderKey);
        _cache.Remove(FooterKey);
    }
}
