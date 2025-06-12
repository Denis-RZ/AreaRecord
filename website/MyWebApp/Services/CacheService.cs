using Microsoft.Extensions.Caching.Memory;
using MyWebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace MyWebApp.Services;

public class CacheService
{
    private readonly IMemoryCache _cache;

    public CacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public T GetOrCreate<T>(string key, Func<ICacheEntry, T> factory)
    {
        return _cache.GetOrCreate(key, factory)!;
    }

    public Task<T> GetOrCreateAsync<T>(string key, Func<ICacheEntry, Task<T>> factory)
    {
        return _cache.GetOrCreateAsync(key, factory)!;
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
    }

    public void WarmCache(ApplicationDbContext db)
    {
        try
        {
            var count = db.Downloads.AsNoTracking().Count(d => d.IsSuccessful);
            _cache.Set(CacheKeys.TotalDownloads, count, TimeSpan.FromMinutes(5));
        }
        catch
        {
            // ignore warming failures
        }
    }
}
