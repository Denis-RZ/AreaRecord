using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MyWebApp.Filters;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

public class BasicAuthAttributeTests
{
    [Fact]
    public void NoHeader_ReturnsUnauthorized()
    {
        var attr = new BasicAuthAttribute();
        var http = new DefaultHttpContext();
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
        var http = new DefaultHttpContext();
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:SecurePass123"));
        http.Request.Headers["Authorization"] = "Basic " + creds;
        var ctx = new AuthorizationFilterContext(
            new ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()),
            new List<IFilterMetadata>());
        attr.OnAuthorization(ctx);
        Assert.Null(ctx.Result);
        Assert.Equal("admin", ctx.HttpContext.User.Identity?.Name);
    }
}
