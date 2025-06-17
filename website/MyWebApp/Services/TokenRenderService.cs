using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;

namespace MyWebApp.Services;

public class TokenRenderService
{
    private static readonly Regex TokenRegex = new(@"\{\{(block|section):([^{}]+)\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public Task<string> RenderAsync(ApplicationDbContext db, string html)
    {
        return RenderAsync(db, html, new HashSet<int>());
    }

    private async Task<string> RenderAsync(ApplicationDbContext db, string html, HashSet<int> stack)
    {
        async Task<string> Replace(Match match)
        {
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
                    var area = parts[1];
                    var htmlParts = await db.PageSections.AsNoTracking()
                        .Where(s => s.PageId == pageId && s.Area == area)
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
