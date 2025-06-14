using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyWebApp.Data;
using MyWebApp.Options;
using System.Collections.Generic;
using Xunit;

public class AdminAuthOptionsSetupTests
{
    [Fact]
    public void OptionsResolveWithoutException()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var services = new ServiceCollection();
        services.AddDbContextFactory<ApplicationDbContext>(o => o.UseSqlite(connection));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"AdminAuth:Username", "admin"},
                {"AdminAuth:Password", "pass"}
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddOptions<AdminAuthOptions>()
            .Bind(config.GetSection("AdminAuth"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Username) && !string.IsNullOrWhiteSpace(o.Password), "Admin credentials required");
        services.AddSingleton<IConfigureOptions<AdminAuthOptions>, AdminAuthOptionsSetup>();
        services.AddSingleton<IPostConfigureOptions<AdminAuthOptions>, AdminAuthOptionsSetup>();
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AdminAuthOptions>>();
        Assert.Equal("admin", options.Value.Username);
        Assert.Equal("pass", options.Value.Password);
    }
}
