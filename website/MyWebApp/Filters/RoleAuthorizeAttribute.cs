using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System;

namespace MyWebApp.Filters
{
    public class RoleAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _roles;
        public RoleAuthorizeAttribute(params string[] roles)
        {
            _roles = roles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var session = context.HttpContext.Session;
            var roles = session.GetString("Roles")?.Split(',') ?? Array.Empty<string>();
            if (!_roles.Any(r => roles.Contains(r)))
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
