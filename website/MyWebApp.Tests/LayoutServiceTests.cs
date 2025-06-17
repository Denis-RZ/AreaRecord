using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using MyWebApp.Services;
using System.Collections.Generic;
using Xunit;

public class LayoutServiceTests
{
    [Fact]
    public void CanReadZonesFromConfig()
    {
        var config = new ConfigurationBuilder().Build();
        var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new CacheService(memory);
        var tokens = new TokenRenderService();
        var service = new LayoutService(cache, tokens);

        Assert.True(LayoutService.LayoutZones.ContainsKey("single-column"));
        Assert.Contains("sidebar", LayoutService.LayoutZones["two-column-sidebar"]);
    }
}
