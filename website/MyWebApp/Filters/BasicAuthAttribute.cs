using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Text;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MyWebApp.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace MyWebApp.Filters
{
    public class BasicAuthAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var services = context.HttpContext.RequestServices;
            var config = services.GetService(typeof(IConfiguration)) as IConfiguration;
            var options = services.GetService(typeof(IOptions<AdminAuthOptions>)) as IOptions<AdminAuthOptions>;
            if (config == null || options == null || !config.GetSection("AdminAuth").Exists())
            {
                throw new InvalidOperationException("AdminAuth configuration missing");
            }

            var creds = options.Value;

            var feature = context.HttpContext.Features.Get<ISessionFeature>();
            var session = feature?.Session;
            var sessionUser = session?.GetString("AdminUser");
            if (session != null && session.GetString("IsAdmin") == "true" && !string.IsNullOrEmpty(sessionUser))
            {
                var identity = new ClaimsIdentity("Session");
                identity.AddClaim(new Claim(ClaimTypes.Name, sessionUser));
                context.HttpContext.User = new ClaimsPrincipal(identity);
                return;
            }

            var request = context.HttpContext.Request;
            if (!request.Headers.TryGetValue("Authorization", out var header))
            {
                Challenge(context);
                return;
            }

            var authHeader = header.ToString();
            if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                var encoded = authHeader.Substring("Basic ".Length).Trim();
                string decoded;
                try
                {
                    decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                }
                catch
                {
                    Challenge(context);
                    return;
                }

                var parts = decoded.Split(':', 2);
                if (parts.Length == 2 && parts[0] == creds.Username && parts[1] == creds.Password)
                {
                    var identity = new ClaimsIdentity("Basic");
                    identity.AddClaim(new Claim(ClaimTypes.Name, creds.Username));
                    context.HttpContext.User = new ClaimsPrincipal(identity);
                    return;
                }
            }

            Challenge(context);
        }

        private static void Challenge(AuthorizationFilterContext context)
        {
            context.HttpContext.Response.Headers["WWW-Authenticate"] = "Basic";
            context.Result = new UnauthorizedResult();
        }
    }
}
