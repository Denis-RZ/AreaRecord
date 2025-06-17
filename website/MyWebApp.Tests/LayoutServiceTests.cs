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
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"Layouts:single-column:0", "main"},
                {"Layouts:two-column-sidebar:0", "main"},
                {"Layouts:two-column-sidebar:1", "sidebar"}
            })
            .Build();
        var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new CacheService(memory);
        var tokens = new TokenRenderService();
        var service = new LayoutService(cache, tokens, config);

        Assert.True(service.LayoutZones.ContainsKey("single-column"));
        Assert.Contains("sidebar", service.LayoutZones["two-column-sidebar"]);
    }
}
