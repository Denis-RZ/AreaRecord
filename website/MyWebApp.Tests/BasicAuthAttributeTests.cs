using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MyWebApp.Filters;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MyWebApp.Options;
using Xunit;
using System.Threading;
using System.Threading.Tasks;

public class BasicAuthAttributeTests
{
    private class DummySession : ISession
    {
        private Dictionary<string, byte[]> _store = new();
        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString();
        public IEnumerable<string> Keys => _store.Keys;
        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value);
    }
    [Fact]
    public void NoHeader_ReturnsUnauthorized()
    {
        var attr = new BasicAuthAttribute();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.Configure<MyWebApp.Options.AdminAuthOptions>(o => { o.Username = "admin"; o.Password = "SecurePass123"; });
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string,string?> { {"AdminAuth:Username","admin"}, {"AdminAuth:Password","SecurePass123"} }).Build());
        var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        var ctx = new AuthorizationFilterContext(
            new ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()),
            new List<IFilterMetadata>());
        attr.OnAuthorization(ctx);
        Assert.IsType<UnauthorizedResult>(ctx.Result);
    }

    [Fact]
    public void ValidHeader_AllowsAccess()
    {
        var attr = new BasicAuthAttribute();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.Configure<MyWebApp.Options.AdminAuthOptions>(o => { o.Username = "admin"; o.Password = "SecurePass123"; });
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string,string?> { {"AdminAuth:Username","admin"}, {"AdminAuth:Password","SecurePass123"} }).Build());
        var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:SecurePass123"));
        http.Request.Headers["Authorization"] = "Basic " + creds;
        var ctx = new AuthorizationFilterContext(
            new ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()),
            new List<IFilterMetadata>());
        attr.OnAuthorization(ctx);
        Assert.Null(ctx.Result);
        Assert.Equal("admin", ctx.HttpContext.User.Identity?.Name);
    }

    [Fact]
    public void WrongHeader_ReturnsUnauthorized()
    {
        var attr = new BasicAuthAttribute();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.Configure<MyWebApp.Options.AdminAuthOptions>(o => { o.Username = "admin"; o.Password = "SecurePass123"; });
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string,string?> { {"AdminAuth:Username","admin"}, {"AdminAuth:Password","SecurePass123"} }).Build());
        var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:wrong"));
        http.Request.Headers["Authorization"] = "Basic " + creds;
        var ctx = new AuthorizationFilterContext(
            new ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()),
            new List<IFilterMetadata>());
        attr.OnAuthorization(ctx);
        Assert.IsType<UnauthorizedResult>(ctx.Result);
    }

    [Fact]
    public void MissingConfig_Throws()
    {
        var attr = new BasicAuthAttribute();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        var ctx = new AuthorizationFilterContext(
            new ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()),
            new List<IFilterMetadata>());
        Assert.Throws<InvalidOperationException>(() => attr.OnAuthorization(ctx));
    }

    [Fact]
    public void Session_AllowsAccess()
    {
        var attr = new BasicAuthAttribute();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.Configure<AdminAuthOptions>(o => { o.Username = "admin"; o.Password = "SecurePass123"; });
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string,string?> { {"AdminAuth:Username","admin"}, {"AdminAuth:Password","SecurePass123"} }).Build());
        var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider, Session = new DummySession() };
        http.Session.SetString("IsAdmin", "true");
        http.Session.SetString("AdminUser", "admin");
        var ctx = new AuthorizationFilterContext(
            new ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()),
            new List<IFilterMetadata>());
        attr.OnAuthorization(ctx);
        Assert.Null(ctx.Result);
        Assert.Equal("admin", ctx.HttpContext.User.Identity?.Name);
    }
}
