using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Linq;
using MyWebApp.Data;

namespace MyWebApp.Services;

public class LayoutService
{
    private readonly CacheService _cache;
    private readonly TokenRenderService _tokens;
    private readonly Dictionary<string, string[]> _zoneMap;
    private const string HeaderKey = "layout_header";
    private const string FooterKey = "layout_footer";

    public IReadOnlyDictionary<string, string[]> LayoutZones => _zoneMap;

    public bool IsValidZone(string layout, string zone)
    {
        return _zoneMap.TryGetValue(layout, out var zones) && zones.Contains(zone);
    }

    public string[] GetZones(string layout)
    {
        return _zoneMap.TryGetValue(layout, out var zones) ? zones : Array.Empty<string>();
    }

    public LayoutService(CacheService cache, TokenRenderService tokens, IConfiguration configuration)
    {
        _cache = cache;
        _tokens = tokens;
        _zoneMap = configuration.GetSection("Layouts").Get<Dictionary<string, string[]>>()
            ?? new Dictionary<string, string[]>();
    }

    public async Task<string> GetHeaderAsync(ApplicationDbContext db)
    {
        return await _cache.GetOrCreateAsync(HeaderKey, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var parts = await db.PageSections.AsNoTracking()
                .Where(s => s.Page.Slug == "layout" && s.Zone == "header")
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
                .Where(s => s.Page.Slug == "layout" && s.Zone == "footer")
                .OrderBy(s => s.SortOrder)
                .Select(s => s.Html)
                .ToListAsync();
            var html = string.Join(System.Environment.NewLine, parts);
            return await _tokens.RenderAsync(db, html);
        });
    }

    public async Task<string> GetSectionAsync(ApplicationDbContext db, int pageId, string zone)
    {

        var parts = await db.PageSections.AsNoTracking()
            .Where(s => s.PageId == pageId && s.Zone == zone)
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
