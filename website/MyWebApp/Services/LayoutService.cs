using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;

namespace MyWebApp.Services;

public class LayoutService
{
    private readonly CacheService _cache;
    private const string HeaderKey = "layout_header";
    private const string FooterKey = "layout_footer";

    public LayoutService(CacheService cache)
    {
        _cache = cache;
    }

    public async Task<string> GetHeaderAsync(ApplicationDbContext db)
    {
        return await _cache.GetOrCreateAsync(HeaderKey, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await db.PageSections.AsNoTracking()
                .Where(s => s.Page.Slug == "layout" && s.Area == "header")
                .Select(s => s.Html)
                .FirstOrDefaultAsync() ?? string.Empty;
        });
    }

    public async Task<string> GetFooterAsync(ApplicationDbContext db)
    {
        return await _cache.GetOrCreateAsync(FooterKey, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await db.PageSections.AsNoTracking()
                .Where(s => s.Page.Slug == "layout" && s.Area == "footer")
                .Select(s => s.Html)
                .FirstOrDefaultAsync() ?? string.Empty;
        });
    }

    public async Task<string> GetSectionAsync(ApplicationDbContext db, int pageId, string area)
    {
        return await db.PageSections.AsNoTracking()
            .Where(s => s.PageId == pageId && s.Area == area)
            .Select(s => s.Html)
            .FirstOrDefaultAsync() ?? string.Empty;
    }

    public void Reset()
    {
        _cache.Remove(HeaderKey);
        _cache.Remove(FooterKey);
    }
}
