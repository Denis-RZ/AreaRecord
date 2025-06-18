using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Linq;
using MyWebApp.Data;

namespace MyWebApp.Services;

public class TokenRenderService
{
    private static readonly Regex TokenRegex = new(@"\{\{(block|section):([^{}]+)\}\}|\{\{nav\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly IHttpContextAccessor _accessor;

    public TokenRenderService(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private string[] GetRoles()
    {
        var roles = _accessor.HttpContext?.Session.GetString("Roles");
        return string.IsNullOrWhiteSpace(roles) ? Array.Empty<string>() : roles.Split(',');
    }

    private async Task<List<int>> GetAllowedPermissionsAsync(ApplicationDbContext db, string[] roles)
    {
        if (roles.Length == 0) return new List<int>();
        return await db.RolePermissions.AsNoTracking()
            .Where(rp => roles.Contains(rp.Role!.Name))
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToListAsync();
    }

    private async Task<List<int>> GetRoleIdsAsync(ApplicationDbContext db, string[] roles)
    {
        if (roles.Length == 0) return new List<int>();
        return await db.Roles.AsNoTracking()
            .Where(r => roles.Contains(r.Name))
            .Select(r => r.Id)
            .ToListAsync();
    }

    public Task<string> RenderAsync(ApplicationDbContext db, string html)
    {
        return RenderAsync(db, html, new HashSet<int>());
    }

    private async Task<string> RenderAsync(ApplicationDbContext db, string html, HashSet<int> stack)
    {
        async Task<string> Replace(Match match)
        {
            if (match.Value.StartsWith("{{nav", StringComparison.OrdinalIgnoreCase))
            {
                var roles = GetRoles();
                var roleIds = await GetRoleIdsAsync(db, roles);
                var pages = await db.Pages.AsNoTracking()
                    .Where(p => p.IsPublished && p.Slug != "layout" && p.Slug != "home" && (p.RoleId == null || roleIds.Contains(p.RoleId.Value)))
                    .OrderBy(p => p.Title)
                    .Select(p => new { p.Slug, p.Title })
                    .ToListAsync();
                var links = pages.Select(p =>
                {
                    var href = p.Slug.Equals("home", StringComparison.OrdinalIgnoreCase) ? "/" : "/" + p.Slug;
                    return $"<a href=\"{href}\">{System.Net.WebUtility.HtmlEncode(p.Title)}</a>";
                });
                return string.Join(" ", links);
            }

            var type = match.Groups[1].Value.ToLowerInvariant();
            var param = match.Groups[2].Value;
            if (type == "block")
            {
                if (!int.TryParse(param, out var id)) return string.Empty;
                if (stack.Contains(id)) return string.Empty;
                stack.Add(id);
                var block = await db.BlockTemplates.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
                var result = block == null ? string.Empty : await RenderAsync(db, block.Html, stack);
                stack.Remove(id);
                return result;
            }
            else if (type == "section")
            {
                var parts = param.Split(':', 2);
                if (parts.Length == 2 && int.TryParse(parts[0], out var pageId))
                {
                    var zone = parts[1];
                    var roles = GetRoles();
                    var roleIds = await GetRoleIdsAsync(db, roles);
                    var allowed = await GetAllowedPermissionsAsync(db, roles);
                    var query = db.PageSections.AsNoTracking()
                        .Where(s => s.PageId == pageId && s.Zone == zone);
                    if (allowed.Count == 0 && roleIds.Count == 0)
                        query = query.Where(s => s.PermissionId == null && s.RoleId == null);
                    else
                        query = query.Where(s =>
                            (s.PermissionId == null || allowed.Contains(s.PermissionId.Value)) &&
                            (s.RoleId == null || roleIds.Contains(s.RoleId.Value)));
                    var htmlParts = await query
                        .OrderBy(s => s.SortOrder)
                        .Select(s => s.Html)
                        .ToListAsync();
                    var combined = string.Join(System.Environment.NewLine, htmlParts);
                    return await RenderAsync(db, combined, stack);
                }
                return string.Empty;
            }
            return string.Empty;
        }

        var result = new System.Text.StringBuilder();
        int last = 0;
        foreach (Match m in TokenRegex.Matches(html))
        {
            result.Append(html, last, m.Index - last);
            result.Append(await Replace(m));
            last = m.Index + m.Length;
        }
        result.Append(html, last, html.Length - last);
        return result.ToString();
    }
}
