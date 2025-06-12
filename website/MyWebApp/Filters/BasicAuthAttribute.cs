using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Text;
using System.Security.Claims;

namespace MyWebApp.Filters
{
    public class BasicAuthAttribute : Attribute, IAuthorizationFilter
    {
        private const string Username = "admin";
        private const string Password = "SecurePass123";

        public void OnAuthorization(AuthorizationFilterContext context)
        {
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
                if (parts.Length == 2 && parts[0] == Username && parts[1] == Password)
                {
                    var identity = new ClaimsIdentity("Basic");
                    identity.AddClaim(new Claim(ClaimTypes.Name, Username));
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
