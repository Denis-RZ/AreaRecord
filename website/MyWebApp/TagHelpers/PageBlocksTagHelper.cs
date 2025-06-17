using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;
using MyWebApp.Services;

namespace MyWebApp.TagHelpers;

[HtmlTargetElement("page-blocks")]
public class PageBlocksTagHelper : TagHelper
{
    private readonly ApplicationDbContext _db;
    private readonly TokenRenderService _tokens;
    public PageBlocksTagHelper(ApplicationDbContext db, TokenRenderService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    public int PageId { get; set; }
    public string Zone { get; set; } = string.Empty;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        var htmlParts = await _db.PageSections.AsNoTracking()
            .Where(s => s.PageId == PageId && s.Zone == Zone)
            .OrderBy(s => s.SortOrder)
            .Select(s => s.Html)
            .ToListAsync();
        var html = string.Join(System.Environment.NewLine, htmlParts);
        html = await _tokens.RenderAsync(_db, html);
        output.Content.SetHtmlContent(html);
    }
}
